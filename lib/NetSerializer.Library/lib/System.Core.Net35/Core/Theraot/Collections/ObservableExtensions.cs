// Needed for NET40

using System;

using Theraot.Core;

namespace Theraot.Collections
{
    public static class ObservableExtensions
    {
        public static IDisposable SubscribeAction<T>(this IObservable<T> observable, Action<T> listener)
        {
            return Check.NotNullArgument(observable, "observable").Subscribe(listener.ToObserver());
        }

        public static IDisposable SubscribeAction<TInput, TOutput>(this IObservable<TInput> observable, Action<TOutput> listener, Converter<TInput, TOutput> converter)
        {
            return Check.NotNullArgument(observable, "observable").Subscribe(listener.ToObserver(converter));
        }

        public static IDisposable SubscribeConverted<TInput, TOutput>(this IObservable<TInput> observable, IObserver<TOutput> observer, Converter<TInput, TOutput> converter)
        {
            return Check.NotNullArgument(observable, "observable").Subscribe(new ConvertedObserver<TInput, TOutput>(observer, converter));
        }

        public static IDisposable SubscribeFiltered<T>(this IObservable<T> observable, IObserver<T> observer, Predicate<T> filter)
        {
            return Check.NotNullArgument(observable, "observable").Subscribe(new FilteredObserver<T>(observer, filter));
        }

        public static IDisposable SubscribeFilteredConverted<TInput, TOutput>(this IObservable<TInput> observable, IObserver<TOutput> observer, Predicate<TInput> filter, Converter<TInput, TOutput> converter)
        {
            return Check.NotNullArgument(observable, "observable").Subscribe(new FilteredConvertedObserver<TInput, TOutput>(observer, filter, converter));
        }

        public static IObserver<T> ToObserver<T>(this Action<T> listener)
        {
            if (listener == null)
            {
                throw new ArgumentNullException("listener");
            }
            return new CustomObserver<T>(listener);
        }

        public static IObserver<TInput> ToObserver<TInput, TOutput>(this Action<TOutput> listener, Converter<TInput, TOutput> converter)
        {
            if (converter == null)
            {
                throw new ArgumentNullException("converter");
            }
            if (listener == null)
            {
                throw new ArgumentNullException("listener");
            }
            return new CustomObserver<TInput>(input => listener(converter(input)));
        }
    }
}