using System;
using System.Collections.Generic;

class MyCuncurrentLinkedList<T>
{
    private LinkedList<T> _list = new LinkedList<T>();
    private object _sync = new object();

    public void AddFirst(T value)
    {
        lock (_sync)
        {
            _list.AddFirst(value);
            
        }
    }

    public void AddLast(T value)
    {
        lock (_sync)
        {
            _list.AddLast(value);

        }
    }
    public LinkedListNode<T> Find(T value)
    {
        lock (_sync)
        {
            return _list.Find(value);
        }
    }

    public void Remove(LinkedListNode<T> _node)
    {
        lock (_sync)
        {
           _list.Remove(_node);
        }
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _list.Count;
            }
        }
    }

    public LinkedListNode<T> First
    {
        get

        {
            lock (_sync)
            {
                return _list.First;
            }
        }
    }
}