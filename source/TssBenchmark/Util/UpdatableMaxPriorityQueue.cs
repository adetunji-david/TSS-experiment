using System.Diagnostics;

namespace TssBenchmark.Util;

public sealed class UpdatableMaxPriorityQueue
{
    private const int Arity = 4;
    private const int Log2Arity = 2;
    private readonly int _capacity;
    private readonly int[] _indexLookup;
    private readonly (int ItemId, double Priority)[] _items;
    public int Count { get; private set; }

    public UpdatableMaxPriorityQueue(int capacity)
    {
        _capacity = capacity;
        _indexLookup = new int[capacity];
        Array.Fill(_indexLookup, -1);
        _items = new (int ItemId, double Priority)[capacity];
    }

    public void CopyTo(UpdatableMaxPriorityQueue other)
    {
        if (_capacity != other._capacity)
        {
            throw new InvalidOperationException();
        }

        other.Count = Count;
        Array.Copy(_items, other._items, _capacity);
        Array.Copy(_indexLookup, other._indexLookup, _capacity);
    }

    public void EnqueueOrUpdate(int itemId, double priority)
    {
        Debug.Assert(itemId >= 0 && itemId < _capacity);
        var index = _indexLookup[itemId];
        switch (index)
        {
            case -1:
                MoveUp((itemId, priority), Count++);
                break;
            case 0:
                MoveDown((itemId, priority), index);
                break;
            default:
                if (_items[GetParentIndex(index)].Priority <= priority)
                {
                    MoveUp((itemId, priority), index);
                }
                else
                {
                    MoveDown((itemId, priority), index);
                }

                break;
        }
    }

    public (int ItemId, double Priority) Dequeue()
    {
        if (Count == 0)
        {
            throw new InvalidOperationException();
        }

        var topItem = _items[0];
        var lastIndex = --Count;
        if (lastIndex > 0)
        {
            MoveDown(_items[lastIndex], 0);
            _items[lastIndex] = default;
        }

        _indexLookup[topItem.ItemId] = -1;
        return topItem;
    }

    public bool TryPeek(out int itemId, out double priority)
    {
        if (Count == 0)
        {
            itemId = default;
            priority = default;
            return false;
        }

        var topItem = _items[0];
        itemId = topItem.ItemId;
        priority = topItem.Priority;
        return true;
    }

    public bool Remove(int itemId)
    {
        Debug.Assert(itemId >= 0 && itemId < _capacity);
        var index = _indexLookup[itemId];
        if (index == -1)
        {
            return false;
        }

        var lastIndex = --Count;
        var replacement = _items[lastIndex];
        _items[lastIndex] = default;

        switch (index)
        {
            case 0:
                if (index < lastIndex)
                {
                    MoveDown(replacement, index);
                }

                break;
            default:
                if (_items[GetParentIndex(index)].Priority <= replacement.Priority)
                {
                    MoveUp(replacement, index);
                }
                else
                {
                    MoveDown(replacement, index);
                }

                break;
        }

        _indexLookup[itemId] = -1;
        return true;
    }

    public bool TryGetPriority(int itemId, out double priority)
    {
        Debug.Assert(itemId >= 0 && itemId < _capacity);
        var index = _indexLookup[itemId];
        if (index == -1)
        {
            priority = default;
            return false;
        }

        priority = _items[index].Priority;
        return true;
    }

    public void Clear()
    {
        if (Count > 0)
        {
            Count = 0;
            Array.Fill(_indexLookup, -1);
        }
    }

    private void MoveUp((int ItemId, double Priority) item, int index)
    {
        var items = _items;
        var indexLookup = _indexLookup;
        while (index > 0)
        {
            var parentIndex = GetParentIndex(index);
            var parent = items[parentIndex];

            if (item.Priority > parent.Priority)
            {
                items[index] = parent;
                indexLookup[parent.ItemId] = index;
                index = parentIndex;
            }
            else
            {
                break;
            }
        }

        items[index] = item;
        indexLookup[item.ItemId] = index;
    }

    private void MoveDown((int ItemId, double Priority) item, int index)
    {
        var size = Count;
        var items = _items;
        var indexLookup = _indexLookup;
        int i;
        while ((i = GetFirstChildIndex(index)) < size)
        {
            var maxChild = items[i];
            var maxChildIndex = i;

            var childIndexUpperBound = Math.Min(i + Arity, size);
            while (++i < childIndexUpperBound)
            {
                var nextChild = items[i];
                if (nextChild.Priority > maxChild.Priority)
                {
                    maxChild = nextChild;
                    maxChildIndex = i;
                }
            }

            if (item.Priority >= maxChild.Priority)
            {
                break;
            }

            items[index] = maxChild;
            indexLookup[maxChild.ItemId] = index;
            index = maxChildIndex;
        }

        items[index] = item;
        indexLookup[item.ItemId] = index;
    }

    private static int GetParentIndex(int index) => (index - 1) >> Log2Arity;

    private static int GetFirstChildIndex(int index) => (index << Log2Arity) + 1;
}