using MOP.Core.DomainObjects;
using MOP.Order.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MOP.Order.Domain.Entities
{
    public class OrderModel : BaseEntity, IAggregateRoot
    {
        // EF Construtor
        public OrderModel()
        {

        }

        public OrderModel(Guid customerId, ShippingType shipping, string observation)
        {
            CustomerId = customerId;
            Shipping = shipping;
            Observation = observation;
            StartedIn = DateTime.Now;
            OrderStatus = OrderStatusType.Analysis;
            Items = new List<OrderItemModel>();
        }

        public Guid CustomerId { get; private set; }
        public DateTime StartedIn { get; private set; }
        public DateTime? FinishedIn { get; private set; }
        public OrderStatusType OrderStatus { get; private set; }
        public ShippingType Shipping { get; private set; }
        public decimal Total { get; private set; }
        public string? Observation { get; private set; }
        public List<OrderItemModel> Items { get; private set; }

        // EF Rela��o        
        public CustomerModel Customer { get; private set; }

        public void DeliverOrder()
        {
            OrderStatus = OrderStatusType.Delivered;
            FinishedIn = DateTime.Now;
        }

        public void FinishOrder()
        {
            OrderStatus = OrderStatusType.Finished;
        }

        public void UpdateItems(List<OrderItemModel> newItems)
        {
            var itemsToAdd = new List<OrderItemModel>();
            var itemsToRemove = new List<OrderItemModel>();

            foreach (var newItem in newItems)
            {
                var oldItem = Items.FirstOrDefault(i => i.ProductId == newItem.ProductId);

                if (oldItem == null)
                {
                    itemsToAdd.Add(newItem);
                }
                else
                {
                    oldItem.Update(newItem.Quantity, newItem.UnitPrice, newItem.Discount, newItem.DiscountValue);
                }
            }

            foreach (var oldItem in Items)
            {
                if (!newItems.Any(i => i.ProductId == oldItem.ProductId))
                {
                    itemsToRemove.Add(oldItem);
                }
            }

            foreach (var item in itemsToAdd)
                Items.Add(item);

            foreach (var item in itemsToRemove)
                item.Delete();
            //var itemToRemove = Items.FirstOrDefault(i => i.ProductId == item.ProductId);
            //Items.Remove(itemToRemove);                                        
        }

        public void CalculateTotal()
        {
            Total = Items.Sum(p => p.CalculateTotal());
        }

        public void Update(Guid customerId, ShippingType shipping, string observation)
        {
            CustomerId = customerId;
            Shipping = shipping;
            Observation = observation;
        }
    }
}