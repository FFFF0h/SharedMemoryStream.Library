﻿// Needed for NET40

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Theraot.Collections.ThreadSafe;

namespace Theraot.Core
{
    [global::System.Diagnostics.DebuggerNonUserCode]
    public static partial class TypeHelper
    {
        public static object[] EmptyObjects
        {
            get
            {
                return ArrayReservoir<object>.EmptyArray;
            }
        }

        public static TTarget As<TTarget>(object source)
            where TTarget : class
        {
            return As
            (
                source,
                new Func<TTarget>
                (
                    () =>
                    {
                        throw new InvalidOperationException("Cannot convert to " + typeof(TTarget).Name);
                    }
                )
            );
        }

        public static TTarget As<TTarget>(object source, TTarget def)
            where TTarget : class
        {
            return As(source, () => def);
        }

        public static TTarget As<TTarget>(object source, Func<TTarget> alternative)
            where TTarget : class
        {
            var _source = source as TTarget;
            var _alternative = Check.NotNullArgument(alternative, "alternative");
            if (_source == null)
            {
                return _alternative();
            }
            else
            {
                return _source;
            }
        }

        public static bool CanBe<T>(this Type type, T value)
        {
            if (object.ReferenceEquals(value, null))
            {
                return type.CanBeNull();
            }
            else
            {
                return value.GetType().IsAssignableTo(type);
            }
        }

        public static bool CanBeNull(this Type type)
        {
            var _type = Check.NotNullArgument(type, "type");
            return !_type.IsValueType || !ReferenceEquals(Nullable.GetUnderlyingType(_type), null);
        }

        public static TTarget Cast<TTarget>(object source)
        {
            return Cast
            (
                source,
                new Func<TTarget>
                (
                    () =>
                    {
                        throw new InvalidOperationException("Cannot convert to " + typeof(TTarget).Name);
                    }
                )
            );
        }

        public static TTarget Cast<TTarget>(object source, TTarget def)
        {
            return Cast(source, () => def);
        }

        public static TTarget Cast<TTarget>(object source, Func<TTarget> alternative)
        {
            var _alternative = Check.NotNullArgument(alternative, "alternative");
            try
            {
                var _source = (TTarget)source;
                return _source;
            }
            catch
            {
                return _alternative();
            }
        }

        public static object Create(this Type type, params object[] arguments)
        {
            return Activator.CreateInstance(type, arguments);
        }

        public static TReturn Default<TReturn>()
        {
            return FuncHelper.GetDefaultFunc<TReturn>().Invoke();
        }

        public static TAttribute[] GetAttributes<TAttribute>(this ICustomAttributeProvider item, bool inherit)
    where TAttribute : Attribute
        {
            return (TAttribute[])Check.NotNullArgument(item, "item").GetCustomAttributes(typeof(TAttribute), inherit);
        }

        public static Func<TReturn> GetDefault<TReturn>()
        {
            return FuncHelper.GetDefaultFunc<TReturn>();
        }

        public static MethodInfo GetDelegateMethodInfo(Type delegateType)
        {
            var _delegateType = Check.NotNullArgument(delegateType, "delegateType");
            if (_delegateType.BaseType != typeof(MulticastDelegate))
            {
                throw new ArgumentException("Not a delegate.");
            }
            else
            {
                var methodInfo = _delegateType.GetMethod("Invoke");
                if (methodInfo == null)
                {
                    throw new ArgumentException("Not a delegate.");
                }
                else
                {
                    return methodInfo;
                }
            }
        }

        public static ParameterInfo[] GetDelegateParameters(Type delegateType)
        {
            return GetDelegateMethodInfo(delegateType).GetParameters();
        }

        public static Type GetDelegateReturnType(Type delegateType)
        {
            return GetDelegateMethodInfo(delegateType).ReturnType;
        }

        public static Type GetNonRefType(this ParameterInfo parameterInfo)
        {
            var parameterType = parameterInfo.ParameterType;
            if (parameterType.IsByRef)
            {
                parameterType = parameterType.GetElementType();
            }
            return parameterType;
        }

        public static Type GetNotNullableType(this Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying == null)
            {
                return type;
            }
            else
            {
                return underlying;
            }
        }

        public static bool HasAttribute<TAttribute>(this ICustomAttributeProvider item)
            where TAttribute : Attribute
        {
            var attributes = item.GetAttributes<TAttribute>(true);
            return attributes != null && attributes.Length > 0;
        }

        public static bool HasConstructor(this Type type, params Type[] typeArguments)
        {
            var constructorInfo = type.GetConstructor(typeArguments);
            return constructorInfo == null;
        }

        public static bool IsArrayTypeAssignableTo(Type type, Type target)
        {
            if (!type.IsArray || !target.IsArray)
            {
                return false;
            }
            else if (type.GetArrayRank() != target.GetArrayRank())
            {
                return false;
            }
            else
            {
                return type.GetElementType().IsAssignableTo(target.GetElementType());
            }
        }

        public static bool IsArrayTypeAssignableToInterface(Type type, Type target)
        {
            if (!type.IsArray)
            {
                return false;
            }
            else
            {
                return
                    (
                        target.IsGenericInstanceOf(typeof(IList<>)) ||
                        target.IsGenericInstanceOf(typeof(ICollection<>)) ||
                        target.IsGenericInstanceOf(typeof(IEnumerable<>))
                    )
                    && type.GetElementType() == target.GetGenericArguments()[0];
            }
        }

        public static bool IsAssignableTo(this Type type, Type target)
        {
            return target.IsAssignableFrom(type)
                || IsArrayTypeAssignableTo(type, target)
                || IsArrayTypeAssignableToInterface(type, target);
        }

        public static bool IsAssignableTo(this Type type, ParameterInfo parameterInfo)
        {
            return IsAssignableTo(GetNotNullableType(type), parameterInfo.GetNonRefType());
        }

        public static bool IsAtomic(Type type)
        {
            return type.IsClass || (type.IsPrimitive && Marshal.SizeOf(type) <= IntPtr.Size);
        }

        public static bool IsBinaryPortable(this Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }
            else
            {
                return IsBinaryPortableExtracted(type);
            }
        }

        public static bool IsBlittable(this Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }
            else
            {
                return IsBlittableExtracted(type);
            }
        }

        public static bool IsGenericImplementationOf(this Type type, Type interfaceGenericTypeDefinition)
        {
            foreach (var currentInterface in type.GetInterfaces())
            {
                if (currentInterface.IsGenericInstanceOf(interfaceGenericTypeDefinition))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsGenericImplementationOf(this Type type, params Type[] interfaceGenericTypeDefinitions)
        {
            foreach (var currentInterface in type.GetInterfaces())
            {
                if (currentInterface.IsGenericTypeDefinition)
                {
                    var match = currentInterface.GetGenericTypeDefinition();
                    if (Array.Exists(interfaceGenericTypeDefinitions, item => item == match))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool IsGenericImplementationOf(this Type type, out Type interfaceType, Type interfaceGenericTypeDefinition)
        {
            foreach (var currentInterface in type.GetInterfaces())
            {
                if (currentInterface.IsGenericInstanceOf(interfaceGenericTypeDefinition))
                {
                    interfaceType = currentInterface;
                    return true;
                }
            }
            interfaceType = null;
            return false;
        }

        public static bool IsGenericImplementationOf(this Type type, out Type interfaceType, params Type[] interfaceGenericTypeDefinitions)
        {
            var implementedInterfaces = type.GetInterfaces();
            foreach (var currentInterface in interfaceGenericTypeDefinitions)
            {
                var index = Array.FindIndex(implementedInterfaces, item => item.IsGenericInstanceOf(currentInterface));
                if (index != -1)
                {
                    interfaceType = implementedInterfaces[index];
                    return true;
                }
            }
            interfaceType = null;
            return false;
        }

        public static bool IsGenericInstanceOf(this Type type, Type genericTypeDefinition)
        {
            if (!type.IsGenericType)
            {
                return false;
            }
            else
            {
                return type.GetGenericTypeDefinition() == genericTypeDefinition;
            }
        }

        public static bool IsImplementationOf(this Type type, Type interfaceType)
        {
            foreach (var currentInterface in type.GetInterfaces())
            {
                if (currentInterface == interfaceType)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsImplementationOf(this Type type, params Type[] interfaceTypes)
        {
            foreach (var currentInterface in type.GetInterfaces())
            {
                if (Array.Exists(interfaceTypes, item => currentInterface == item))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsImplementationOf(this Type type, out Type interfaceType, params Type[] interfaceTypes)
        {
            var implementedInterfaces = type.GetInterfaces();
            foreach (var currentInterface in interfaceTypes)
            {
                var index = Array.FindIndex(implementedInterfaces, item => item == currentInterface);
                if (index != -1)
                {
                    interfaceType = implementedInterfaces[index];
                    return true;
                }
            }
            interfaceType = null;
            return false;
        }

        public static bool IsNullable(this Type type)
        {
            return !ReferenceEquals(Nullable.GetUnderlyingType(type), null);
        }

        public static bool IsPrimitiveInteger(this Type type)
        {
            if
                (
                    type == typeof(sbyte)
                    || type == typeof(byte)
                    || type == typeof(short)
                    || type == typeof(int)
                    || type == typeof(long)
                    || type == typeof(ushort)
                    || type == typeof(uint)
                    || type == typeof(ulong)
                )
            {
                return true;
            }
            return false;
        }

        public static bool IsSameOrSubclass(this Type type, Type subType)
        {
            if (type.IsGenericType)
            {
                /*while (subType != null)
                {
                    if (subType.IsGenericType)
                    {
                        subType = subType.GetGenericTypeDefinition();
                        if (type == subType)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        subType = subType.BaseType;
                    }
                }*/
                return false;
            }
            else
            {
                while (subType != null)
                {
                    if (type == subType)
                    {
                        return true;
                    }
                    subType = subType.BaseType;
                }
            }
            return false;
        }

        public static bool IsValueTypeRecursive(this Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }
            else
            {
                return IsValueTypeRecursiveExtracted(type);
            }
        }

        public static Type MakeNullableType(this Type self)
        {
            return typeof(Nullable<>).MakeGenericType(self);
        }

        private static bool IsBinaryPortableExtracted(Type type)
        {
            var property = typeof(BinaryPortableInfo<>).MakeGenericType(type).GetProperty("Result", BindingFlags.Public | BindingFlags.Static);
            return (bool)property.GetValue(null, null);
        }

        private static bool IsBlittableExtracted(Type type)
        {
            var property = typeof(BlittableInfo<>).MakeGenericType(type).GetProperty("Result", BindingFlags.Public | BindingFlags.Static);
            return (bool)property.GetValue(null, null);
        }

        private static bool IsValueTypeRecursiveExtracted(Type type)
        {
            var property = typeof(ValueTypeInfo<>).MakeGenericType(type).GetProperty("Result", BindingFlags.Public | BindingFlags.Static);
            return (bool)property.GetValue(null, null);
        }

        private static class BinaryPortableInfo<T>
        {
            private static readonly bool _result;

            static BinaryPortableInfo()
            {
                var type = typeof(T);
                if (type.IsPrimitive)
                {
                    if
                    (
                        type == typeof(IntPtr) ||
                        type == typeof(UIntPtr) ||
                        type == typeof(char) ||
                        type == typeof(bool)
                    )
                    {
                        _result = false;
                    }
                    else
                    {
                        _result = true;
                    }
                }
                else
                {
                    if (type.IsValueType)
                    {
                        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            if (!IsBinaryPortableExtracted(field.FieldType))
                            {
                                _result = false;
                                return;
                            }
                        }
                        var attributes = (StructLayoutAttribute[])type.GetCustomAttributes(typeof(StructLayoutAttribute), true);
                        _result = attributes.Length > 0 && attributes[0].Value != LayoutKind.Auto && attributes[0].Pack > 0;
                    }
                    else
                    {
                        _result = false;
                    }
                }
            }

            public static bool Result
            {
                // Accessed via Reflection
                get
                {
                    return _result;
                }
            }
        }

        private static class BlittableInfo<T>
        {
            private static readonly bool _result;

            static BlittableInfo()
            {
                var type = typeof(T);
                if (type.IsPrimitive)
                {
                    if
                    (
                        type == typeof(char) ||
                        type == typeof(bool)
                    )
                    {
                        _result = false;
                    }
                    else
                    {
                        _result = true;
                    }
                }
                else
                {
                    if (type.IsValueType)
                    {
                        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            if (!IsBlittableExtracted(field.FieldType))
                            {
                                _result = false;
                                return;
                            }
                        }
                        _result = true;
                    }
                    else
                    {
                        _result = false;
                    }
                }
            }

            public static bool Result
            {
                // Accessed via Reflection
                get
                {
                    return _result;
                }
            }
        }

        private static class ValueTypeInfo<T>
        {
            private static readonly bool _result;

            static ValueTypeInfo()
            {
                var type = typeof(T);
                if (type.IsPrimitive)
                {
                    _result = true;
                }
                else
                {
                    if (type.IsValueType)
                    {
                        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            if (!IsValueTypeRecursiveExtracted(field.FieldType))
                            {
                                _result = false;
                                return;
                            }
                        }
                        _result = true;
                    }
                    else
                    {
                        _result = false;
                    }
                }
            }

            public static bool Result
            {
                // Accessed via Reflection
                get
                {
                    return _result;
                }
            }
        }
    }

#if NET35 || NET40

    public static partial class TypeHelper
    {
        public static object GetValue(this PropertyInfo info, object obj)
        {
            //Added in .NET 4.5
            return info.GetValue(obj, null);
        }

        public static void SetValue(this PropertyInfo info, object obj, object value)
        {
            //Added in .NET 4.5
            info.SetValue(obj, value, null);
        }
    }

#endif
}