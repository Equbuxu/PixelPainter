using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter
{
    public class OrderedSet<T> : ICollection<T>
    {
        private readonly IDictionary<T, LinkedListNode<T>> nodeDictionary;
        private readonly LinkedList<T> linkedList;

        public OrderedSet()
            : this(EqualityComparer<T>.Default)
        {
        }

        public OrderedSet(IEqualityComparer<T> comparer)
        {
            nodeDictionary = new Dictionary<T, LinkedListNode<T>>(comparer);
            linkedList = new LinkedList<T>();
        }

        public int Count
        {
            get { return nodeDictionary.Count; }
        }

        public virtual bool IsReadOnly
        {
            get { return nodeDictionary.IsReadOnly; }
        }

        void ICollection<T>.Add(T item)
        {
            Add(item);
        }

        public bool Add(T item)
        {
            if (nodeDictionary.ContainsKey(item)) return false;
            LinkedListNode<T> node = linkedList.AddLast(item);
            nodeDictionary.Add(item, node);
            return true;
        }

        public void Clear()
        {
            linkedList.Clear();
            nodeDictionary.Clear();
        }

        public bool Remove(T item)
        {
            LinkedListNode<T> node;
            bool found = nodeDictionary.TryGetValue(item, out node);
            if (!found) return false;
            nodeDictionary.Remove(item);
            linkedList.Remove(node);
            return true;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return linkedList.GetEnumerator();
        }

        public bool Contains(T item)
        {
            return nodeDictionary.ContainsKey(item);
        }

        public T GetStoredCopy(T item)
        {
            return nodeDictionary[item].Value;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            linkedList.CopyTo(array, arrayIndex);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
