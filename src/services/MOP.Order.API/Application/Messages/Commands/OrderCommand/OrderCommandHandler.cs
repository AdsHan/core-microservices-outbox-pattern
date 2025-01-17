﻿using MediatR;
using MOP.Core.Commands;
using MOP.Core.Communication;
using MOP.IntegrationEventLog.Services;
using MOP.MessageBus;
using MOP.MessageBus.Integration;
using MOP.Order.Domain.Entities;
using MOP.Order.Domain.Repositories;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MOP.Order.API.Application.Messages.Commands.OrderCommand
{
    public class OrderCommandHandler : CommandHandler,
        IRequestHandler<CreateOrderCommand, BaseResult>,
        IRequestHandler<UpdateOrderCommand, BaseResult>,
        IRequestHandler<DeleteOrderCommand, BaseResult>
    {
        private readonly IMessageBusService _messageBusService;
        private readonly IOrderRepository _orderRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IIntegrationEventLogRepository _integrationEventLogRepository;

        private Guid idEvent;

        public OrderCommandHandler(IOrderRepository orderRepository, ICustomerRepository customerRepository, IMessageBusService messageBusService, IIntegrationEventLogRepository integrationEventLogRepository)
        {
            _orderRepository = orderRepository;
            _customerRepository = customerRepository;
            _messageBusService = messageBusService;
            _integrationEventLogRepository = integrationEventLogRepository;
        }

        public async Task<BaseResult> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
        {
            if (!command.Validate()) return command.BaseResult;

            var customer = await _customerRepository.GetByIdAsync(command.CustomerId);

            if (customer == null)
            {
                AddError("O cliente informado não foi encontrado!");
                return BaseResult;
            }

            var order = new OrderModel(command.CustomerId, command.Shipping, command.Observation);

            var items = command.Items.Select(i => new OrderItemModel(order.Id, i.ProductId, i.Quantity, i.UnitPrice, i.Discount, i.DiscountValue)).ToList();
            order.UpdateItems(items);

            order.CalculateTotal();

            _orderRepository.Add(order);

            try
            {
                var evt = new OrderCreatedIntegrationEvent(order.Id, customer.Name, customer.Email);
                idEvent = await _integrationEventLogRepository.SaveAsync(order.Id, order.GetType().Name, evt);

                await _orderRepository.SaveAsync();

                _messageBusService.Publish(ExchangeType.NOTIFICATION, QueueTypes.NOTIFICATION_ORDER_CREATED, evt);

                await _integrationEventLogRepository.MarkEventAsPublishedAsync(idEvent);

                BaseResult.response = order.Id;
            }
            catch (Exception ex)
            {
                await _integrationEventLogRepository.MarkEventAsNoPublishedAsync(idEvent);
                AddError("Erro ao publicar menssagem");
            }

            return BaseResult;
        }

        public async Task<BaseResult> Handle(UpdateOrderCommand command, CancellationToken cancellationToken)
        {
            if (!command.Validate()) return command.BaseResult;

            var order = await _orderRepository.GetByIdAsync(command.Id);

            if (order == null)
            {
                AddError("Não foi possível localizar o atendimento!");
                return BaseResult;
            }

            order.Update(command.CustomerId, command.Shipping, command.Observation);

            var newItems = command.Items.Select(i => new OrderItemModel(order.Id, i.ProductId, i.Quantity, i.UnitPrice, i.Discount, i.DiscountValue)).ToList();

            order.UpdateItems(newItems);

            order.CalculateTotal();

            _orderRepository.Update(order);

            try
            {
                await _orderRepository.SaveAsync();
            }
            catch (Exception ex)
            {
                AddError("Erro ao salvar o atendimento");
            }

            return BaseResult;
        }

        public async Task<BaseResult> Handle(DeleteOrderCommand command, CancellationToken cancellationToken)
        {
            if (!command.Validate()) return command.BaseResult;

            var order = await _orderRepository.GetByIdAsync(command.Id);

            if (order == null)
            {
                AddError("Não foi possível localizar o atendimento!");
                return BaseResult;
            }

            order.Delete();

            _orderRepository.Update(order);

            await _orderRepository.SaveAsync();

            return BaseResult;
        }

    }
}