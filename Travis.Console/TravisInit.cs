﻿using Spring.Context;
using Spring.Context.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Travis.Logic.Extensions;

namespace Travis.Console
{
    /// <summary>
    /// Exception thrown when object couldn't be constructed.
    /// </summary>
    public class ObjectConstructionException : Exception
    {
        /// <summary>
        /// Creates new instance of <see cref="ObjectConstructionException"/> with specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ObjectConstructionException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Class used to build objects registered in spring.
    /// </summary>
    public class TravisInit
    {
        private IApplicationContext context;
        private static TravisInit _instance;

        /// <summary>
        /// Gets curent <see cref="TravisInit"/> instance.
        /// </summary>
        public static TravisInit Current
        {
            get
            {
                if (_instance == null)
                    _instance = new TravisInit();
                return _instance;
            }
        }

        private TravisInit()
        {
            context = ContextRegistry.GetContext();
        }

        /// <summary>
        /// Gets object registered with <paramref name="name"/>.
        /// </summary>
        /// <typeparam name="T">Type of object to get.</typeparam>
        /// <param name="name">Registered name.</param>
        public T GetObject<T>(string name)
        {
            GetType<T>(name);
            return context.GetObject<T>(name);
        }

        /// <summary>
        /// Gets object registered with <paramref name="name"/>.
        /// </summary>
        /// <param name="name">Registered name.</param>
        public object GetObject(string name)
        {
            return context.GetObject(name);
        }

        private Type GetType<T>(string name)
        {
            var type = context.GetType(name);
            if (!context.ContainsObject(name))
                throw new ObjectConstructionException(Messages.ObjectNotRegistered.FormatString(name));
            if (!typeof(T).IsAssignableFrom(type))
                throw new ObjectConstructionException(Messages.InvalidObjectType.FormatString(name, type.Name, typeof(T).Name));
            return type;
        }

        /// <summary>
        /// Gets object registered with <paramref name="name"/> using given constructor <paramref name="arguments"/>.
        /// </summary>
        /// <typeparam name="T">Type of object to get.</typeparam>
        /// <param name="name">Registered name.</param>
        /// <param name="arguments">Named constructor arguments.</param>
        public T GetObject<T>(string name, IList<KeyValuePair<string, string>> arguments)
        {
            var type = GetType<T>(name);
            var typeConstructor = FindConstructor(type, arguments);
            if (typeConstructor == null)
                throw new ObjectConstructionException(Messages.ConstructorNotFound.FormatString(typeof(T).Name, name,
                    arguments.Select(kv => kv.Key + " = " + kv.Value).JoinString(", ")));
            var args = CastArguments(typeConstructor, arguments);
            return context.GetObject<T>(name, args);
        }

        private object[] CastArguments(ConstructorInfo typeConstructor, IList<KeyValuePair<string, string>> arguments)
        {
            var result = new object[arguments.Count];
            var parameterInfos = typeConstructor.GetParameters();
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Convert.ChangeType(arguments[i].Value, parameterInfos[i].ParameterType);
            }
            return result;
        }

        private static ConstructorInfo FindConstructor(Type type, IList<KeyValuePair<string, string>> arguments)
        {
            var typeConstructors = type.GetConstructors().Where(ctor => ctor.IsPublic && MatchParameters(ctor.GetParameters(), arguments));
            return typeConstructors.FirstOrDefault();
        }

        private static bool MatchParameters(ParameterInfo[] parameters, IList<KeyValuePair<string, string>> arguments)
        {
            if (parameters.Length != arguments.Count)
                return false;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].Name != arguments[i].Key)
                    return false;
            }
            return true;
        }
    }
}
