﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitRx.Message;

namespace RabbitRx.Client
{
    public class ObservableConsumer<TData> : ISubject<IRabbitMessage<TData>>, IBasicConsumer, IDisposable
    {
        public ObservableConsumer(IModel model, QueueSettings queueSettings)
        {
            Model = model;
            QueueSettings = queueSettings;
        }       

        public IModel Model { get; private set; }

        public QueueSettings QueueSettings { get; private set; }

        private ICollection<IObserver<IRabbitMessage<TData>>> observers = new List<IObserver<IRabbitMessage<TData>>>();
        
        public bool IsRunning { get; protected set; }

        public string ConsumerTag { get; set; }

        public event ConsumerCancelledEventHandler ConsumerCancelled;

        public virtual IDisposable Subscribe(IObserver<IRabbitMessage<TData>> observer)
        {
            if (!IsRunning)
            {
                Model.BasicQos(0, 100, false);
                Model.BasicConsume(QueueSettings.Name, QueueSettings.NoAck, QueueSettings.ConsumerName, this);
                IsRunning = true;
            }

            if (!observers.Contains(observer))
                observers.Add(observer);

            return new Unsubscriber(observers, observer);
        }

        public void HandleBasicConsumeOk(string consumerTag)
        {
            ConsumerTag = consumerTag;
        }

        public void HandleBasicCancelOk(string consumerTag)
        {
            Cancel();
        }

        public void HandleBasicCancel(string consumerTag)
        {
            Cancel();
        }

        public void HandleModelShutdown(IModel model, ShutdownEventArgs reason)
        {
            Cancel();
        }

        private void Cancel()
        {
            if (ConsumerCancelled != null)
            {
                ConsumerCancelled(this, new ConsumerEventArgs(ConsumerTag));
            }
        }

        public void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey,
            IBasicProperties properties, byte[] body)
        {
            var message = new JsonRabbitMessage<TData>
            {
                ConsumerTag = consumerTag,
                DeliveryTag = deliveryTag,
                Redelivered = redelivered,
                Exchange = exchange,
                RoutingKey = routingKey,
                Properties = properties,
                RawBody = body
            };

            OnNext(message);
        }


        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public virtual void OnNext(IRabbitMessage<TData> value)
        {
            Publish(value);
        }

        protected virtual void Publish(IRabbitMessage<TData> value)
        {
            foreach (var observer in observers)
            {
                observer.OnNext(value);
            }
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnCompleted()
        {
            throw new NotImplementedException();
        }

        private class Unsubscriber : IDisposable
        {
            private ICollection<IObserver<IRabbitMessage<TData>>> _observers;
            private IObserver<IRabbitMessage<TData>> _observer;

            public Unsubscriber(ICollection<IObserver<IRabbitMessage<TData>>> observers, IObserver<IRabbitMessage<TData>> observer)
            {
                this._observers = observers;
                this._observer = observer;
            }

            public void Dispose()
            {
                if (_observer != null && _observers.Contains(_observer))
                    _observers.Remove(_observer);
            }
        }

    }

}
