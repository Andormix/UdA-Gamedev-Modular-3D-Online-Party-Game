using System;
using System.Collections.Generic;

public class ServiceLocator
{
    private static Dictionary<Type, object> services = new Dictionary<Type, object>();

    public static void Register<T> (T service ) where T : class
    {
        services[typeof (T)] = service;
    }

    public static void Unregister<T>() where T : class
    {
        services.Remove(typeof(T));
    }

    public static T Get<T>() where T : class
    {
        if (services.TryGetValue(typeof (T), out object service))
        {
            return service as T;
        }

        throw new Exception ("El servei " + typeof (T).Name + " no trobat");
    }
}
