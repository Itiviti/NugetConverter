using System.Collections.ObjectModel;
using System.Linq;

namespace Ullink.NugetConverter.utils
{
    public static class ObservableCollectionMixins
    {
        public static void Enqueue<T>(this ObservableCollection<T> collection, T item)
        {
            collection.Add(item);
        }

        /// <summary>
        /// Not really atomic...
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public static T Dequeue<T>(this ObservableCollection<T> collection, T item)
        {
            T lastItem = collection.Last();
            collection.RemoveAt(collection.Count-1);
            return lastItem;
        }
    }
}
