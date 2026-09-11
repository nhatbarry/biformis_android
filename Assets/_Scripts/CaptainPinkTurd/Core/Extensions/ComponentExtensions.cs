using System.Collections.Generic;
using UnityEngine;

namespace CaptainPinkTurd.Core.Extensions
{
    public static class ComponentExtensions
    {
        /// <summary>
        /// Tries to find a component of type T on this Component,
        /// its parents, or its children. Returns true if found.
        /// </summary>
        public static bool TryGetComponentInHierarchy<T>(this Component obj, out T component, 
            bool includeInactiveChildren = false)
        {
            // 1. Check on the object itself
            if (obj.TryGetComponent(out component))
                return true;

            // 2. Check in parents (includes this object but already checked)
            component = obj.GetComponentInParent<T>(includeInactive: true);
            if (component != null)
                return true;

            // 3. Check children
            component = obj.GetComponentInChildren<T>(includeInactiveChildren);
            if (component != null)
                return true;

            // Nothing found
            return false;
        }
        
        /// <summary>
        /// Gets all components of type T on this Component, its parents, and its children.
        /// Each component is included only once, even if reachable via multiple paths.
        /// </summary>
        public static List<T> GetComponentsInHierarchy<T>(this Component obj, bool includeInactive = false)
        {
            HashSet<T> results = new HashSet<T>();

            results.UnionWith(obj.GetComponents<T>());
            results.UnionWith(obj.GetComponentsInParent<T>(includeInactive));
            results.UnionWith(obj.GetComponentsInChildren<T>(includeInactive));

            List<T> list = new List<T>(results.Count);
            list.AddRange(results);
            return list;
        }

        /// <summary>
        /// Tries to get a component of type T from the parents of this Component.
        /// Does NOT check the Component itself.
        /// </summary>
        public static bool TryGetComponentInParents<T>(this Component obj, out T component) 
        {
            // Unity's GetComponentInParent includes the object itself,
            // so we manually walk the hierarchy instead.
            Transform current = obj.transform.parent;

            while (current)
            {
                if (current.TryGetComponent(out component))
                    return true;

                current = current.parent;
            }

            component = default(T);
            return false;
        }
        
        /// <summary>
        /// Tries to get a component of type T from the children of this Component.
        /// Does NOT check the Component itself.
        /// </summary>
        public static bool TryGetComponentInChildrenOnly<T>(this Component obj, out T component,
            bool includeInactive = false)
        {
            // Unity’s GetComponentInChildren includes the object itself,
            // so we manually iterate children.
            foreach (Transform child in obj.transform)
            {
                // Check this child
                if (child.TryGetComponent(out component))
                    return true;

                // Recursively search grandchildren
                if (TryGetComponentInChildrenRecursive(child, out component, includeInactive))
                    return true;
            }

            component = default(T);
            return false;
            
            static bool TryGetComponentInChildrenRecursive(Transform current, out T component, bool includeInactive) 
            {
                // Skip inactive branches if requested
                if (!includeInactive && !current.gameObject.activeInHierarchy)
                {
                    component = default(T);
                    return false;
                }

                // Check children of current transform
                foreach (Transform child in current)
                {
                    if (child.TryGetComponent(out component))
                        return true;

                    if (TryGetComponentInChildrenRecursive(child, out component, includeInactive))
                        return true;
                }

                component = default(T);
                return false;
            }
        }
    }
}