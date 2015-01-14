﻿using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitRx.Subscription
{
    public class SubscriptionConsumer : RabbitMQ.Client.MessagePatterns.Subscription
    {
        protected readonly Subject<BasicDeliverEventArgs> Subject = new Subject<BasicDeliverEventArgs>();

        protected SubscriptionConsumer(IModel model, string queueName)
            : base(model, queueName)
        {
        }

        protected SubscriptionConsumer(IModel model, string queueName, bool noAck)
            : base(model, queueName, noAck)
        {
        }

        protected SubscriptionConsumer(IModel model, string queueName, bool noAck, string consumerTag)
            : base(model, queueName, noAck, consumerTag)
        {
        }

        public virtual Task Start(CancellationToken token, int? timeout = null, Action onQueueEmpty = null)
        {
            var task = new Task(() => Consume(token, timeout, onQueueEmpty), token, TaskCreationOptions.LongRunning);
            task.Start(TaskScheduler.Default);
            return task;
        }

        protected virtual void Consume(CancellationToken token, int? timeout = null, Action onQueueEmpty = null)
        {
            token.Register(Close); //This breaks the block below
            
            while (true)
            {
                try
                {
                    BasicDeliverEventArgs evt;
                    if (timeout.HasValue)
                    {
                        Next(timeout.Value, out evt);
                    }
                    else
                    {
                        evt = Next(); //Blocking de-queue
                    }

                    if (token.IsCancellationRequested) break;

                    if (evt != null)
                    {
                        Subject.OnNext(evt); //Publish
                    }
                    else if (onQueueEmpty != null)
                    {
                        onQueueEmpty();
                    }
                }
                catch (Exception ex)
                {
                    Subject.OnError(ex);
                }
            }

            Subject.OnCompleted(); //End of stream
        }
    }
}