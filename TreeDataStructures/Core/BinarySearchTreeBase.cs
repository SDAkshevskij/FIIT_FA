using System.Collections;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Xml;
using TreeDataStructures.Interfaces;

namespace TreeDataStructures.Core;

public abstract class BinarySearchTreeBase<TKey, TValue, TNode>(IComparer<TKey>? comparer = null)
    : ITree<TKey, TValue>
    where TNode : Node<TKey, TValue, TNode>
{
    protected TNode? Root;
    public IComparer<TKey> Comparer { get; protected set; } = comparer ?? Comparer<TKey>.Default; // use it to compare Keys

    public int Count { get; protected set; }

    public bool IsReadOnly => false;

    public ICollection<TKey> Keys
    {
        get
        {
            List<TKey> keys = new List<TKey>(this.Count);
            foreach (var entry in InOrder())
            {
                keys.Add(entry.Key);
            }
            return keys;
        }
    }
    public ICollection<TValue> Values
    {
        get
        {
            List<TValue> values = new List<TValue>(this.Count);
            foreach (var entry in InOrder())
            {
                values.Add(entry.Value);
            }
            return values;
        }
    }
    
    
    public virtual void Add(TKey key, TValue value)
    {
        TNode node = CreateNode(key, value);

        if (Root == null) {
            Root = node;
            OnNodeAdded(node);
            Count++;
            return;
        }

        int cmp = 0;
        TNode prev = Root;
        TNode? current = Root;

        while (current != null)
        {
            cmp = Comparer.Compare(node.Key, current.Key);
            if (cmp == 0)
            {
                current.Value = value;
                return;
            }
            prev = current;
            current = cmp < 0 ? current.Left : current.Right;
        }

        if (cmp < 0)
        {
            prev.Left = node;
        }
        else
        {
            prev.Right = node;
        }
        node.Parent = prev;
        Count++;
        OnNodeAdded(node);
    }

    
    public virtual bool Remove(TKey key)
    {
        TNode? node = FindNode(key);
        if (node == null) { return false; }

        RemoveNode(node);
        this.Count--;
        return true;
    }
    
    
    protected virtual void RemoveNode(TNode node)
    {
        if (node.Left == null && node.Right == null)
        {
            Transplant(node, null);
            OnNodeRemoved(node.Parent, null);
        }
        else if (node.Left != null && node.Right != null)
        {
            TNode receiver = GetSmallestSubtreeNode(node.Right);
            node.Value = receiver.Value;
            node.Key = receiver.Key;
            RemoveNode(receiver);
        }
        else if (node.Left != null)
        {
            Transplant(node, node.Left);
            OnNodeRemoved(node.Parent, node.Left);
        }
        else
        {
            Transplant(node, node.Right);
            OnNodeRemoved(node.Parent, node.Right);
        }
    }

    public virtual bool ContainsKey(TKey key) => TryGetValue(key, out _);
    
    public virtual bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        TNode? node = FindNode(key);
        if (node != null)
        {
            value = node.Value;
            return true;
        }
        value = default;
        return false;
    }

    public TValue this[TKey key]
    {
        get => TryGetValue(key, out TValue? val) ? val : throw new KeyNotFoundException();
        set => Add(key, value);
    }

    
    #region Hooks
    
    /// <summary>
    /// Вызывается после успешной вставки
    /// </summary>
    /// <param name="newNode">Узел, который встал на место</param>
    protected virtual void OnNodeAdded(TNode newNode) { }
    
    /// <summary>
    /// Вызывается после удаления. 
    /// </summary>
    /// <param name="parent">Узел, чей ребенок изменился</param>
    /// <param name="child">Узел, который встал на место удаленного</param>
    protected virtual void OnNodeRemoved(TNode? parent, TNode? child) { }
    
    #endregion
    
    
    #region Helpers
    protected abstract TNode CreateNode(TKey key, TValue value);
    
    
    protected TNode? FindNode(TKey key)
    {
        TNode? current = Root;
        while (current != null)
        {
            int cmp = Comparer.Compare(key, current.Key);
            if (cmp == 0) { return current; }
            current = cmp < 0 ? current.Left : current.Right;
        }
        return null;
    }

    protected void RotateLeft(TNode x)
    {
        if (x == null || x.Right == null)
        {
            return;
        }

        TNode? mainParent = x.Parent;
        TNode right = x.Right;

        if (x.IsLeftChild)
        {
            mainParent?.Left = right;
        }
        else mainParent?.Right = right;

        x.Right = right.Left;
        x.Right?.Parent = x;
        x.Parent = right;
        right.Left = x;
        right.Parent = mainParent;

        if (mainParent == null)
        {
            Root = right;
        }
    }

    protected void RotateRight(TNode y)
    {
        if (y == null || y.Left == null)
        {
            return;
        }

        TNode? mainParent = y.Parent;
        TNode left = y.Left;

        if (y.IsLeftChild) mainParent?.Left = left;
        else mainParent?.Right = left;

        y.Left = left.Right;
        y.Left?.Parent = y;
        y.Parent = left;
        left.Right = y;
        left.Parent = mainParent;

        if (mainParent == null)
        {
            Root = left;
        }
    }
    
    protected void RotateBigLeft(TNode x)
    {
        if (x == null) return;
        TNode? rightChild = x.Right;
        if (rightChild == null) return;
        RotateRight(rightChild);
        RotateLeft(x);
    }
    
    protected void RotateBigRight(TNode y)
    {
        if (y == null) return;
        TNode? leftChild = y.Left;
        if (leftChild == null) return;
        RotateLeft(leftChild);
        RotateRight(y);
    }
    
    protected void RotateDoubleLeft(TNode x)
    {
        RotateLeft(x.Right!);
        RotateLeft(x);
    }
    
    protected void RotateDoubleRight(TNode y)
    {
        RotateRight(y.Left!);
        RotateRight(y);
    }
    
    protected void Transplant(TNode u, TNode? v)
    {
        if (u.Parent == null)
        {
            Root = v;
        }
        else if (u.IsLeftChild)
        {
            u.Parent.Left = v;
        }
        else
        {
            u.Parent.Right = v;
        }
        v?.Parent = u.Parent;
    }

    protected TNode GetSmallestSubtreeNode(TNode node)
    {
        while (node.Left != null)
        {
            node = node.Left;
        }
        return node;
    }
    #endregion

    public IEnumerable<TreeEntry<TKey, TValue>> InOrder() => InOrderTraversal(Root, 0);

    private IEnumerable<TreeEntry<TKey, TValue>> InOrderTraversal(TNode? node, int depth)
    {
        if (node == null)
            yield break;

        foreach (var leftItem in InOrderTraversal(node.Left, depth + 1))
            yield return leftItem;

        yield return new TreeEntry<TKey, TValue>(node.Key, node.Value, depth);

        foreach (var rightItem in InOrderTraversal(node.Right, depth + 1))
            yield return rightItem;
    }

    public IEnumerable<TreeEntry<TKey, TValue>>  PreOrder()
    {
        if (Root == null)
            return Enumerable.Empty<TreeEntry<TKey, TValue>>();
        return new TreeIterator(this.Root, TraversalStrategy.PreOrder);
    }
    public IEnumerable<TreeEntry<TKey, TValue>>  PostOrder()
    {
        if (Root == null)
            return Enumerable.Empty<TreeEntry<TKey, TValue>>();
        return new TreeIterator(Root, TraversalStrategy.PostOrder);
    }
    public IEnumerable<TreeEntry<TKey, TValue>>  InOrderReverse()
    {
        if (Root == null)
            return Enumerable.Empty<TreeEntry<TKey, TValue>>();
        return new TreeIterator(Root, TraversalStrategy.InOrderReverse);
    }
    public IEnumerable<TreeEntry<TKey, TValue>>  PreOrderReverse()
    {
        if (Root == null)
            return Enumerable.Empty<TreeEntry<TKey, TValue>>();
        return new TreeIterator(Root, TraversalStrategy.PostOrderReverse);
    }
    public IEnumerable<TreeEntry<TKey, TValue>>  PostOrderReverse()
    {
        if (Root == null)
            return Enumerable.Empty<TreeEntry<TKey, TValue>>();
        return new TreeIterator(Root, TraversalStrategy.PreOrderReverse);
    }
    
    /// <summary>
    /// Внутренний класс-итератор. 
    /// Реализует паттерн Iterator вручную, без yield return (ban).
    /// </summary>
    private struct TreeIterator : 
        IEnumerable<TreeEntry<TKey, TValue>>,
        IEnumerator<TreeEntry<TKey, TValue>>
    {
        private TNode? curNode;
        private TNode? prevNode;
        private int curDepth;
        private readonly TraversalStrategy _strategy;
        private readonly TNode _root;
        private TreeEntry<TKey, TValue> _current;
        public IEnumerator<TreeEntry<TKey, TValue>> GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => this;
        
        public TreeEntry<TKey, TValue> Current => _current;
        object IEnumerator.Current => Current;
        
        public TreeIterator(TNode root, TraversalStrategy strategy)
        {
            _current = default;
            this._strategy = strategy;
            this._root = root;
            if (root == null) return;
            Reset();
        }

        private TNode MoveFullLeft(TNode node)
        {
            while(node.Left != null)
            {
                node = node.Left;
                curDepth++;
            }
            return node;
        }
        private TNode MoveFullRight(TNode node)
        {
            while (node.Right != null)
            {
                node = node.Right;
                curDepth++;
            }
            return node;
        }
        private TNode MoveFullLeftRight(TNode node, bool reverse)
        {
            if (reverse)
            {
                while (node.Left != null || node.Right != null)
                {
                    if (node.Right == null) node = node.Left!;
                    else node = node.Right;
                    curDepth++;
                }
            }
            else
            {
                while (node.Left != null || node.Right != null)
                {
                    if (node.Left == null) node = node.Right!;
                    else node = node.Left;
                    curDepth++;
                }
            }
            return node;
        }
        private bool MoveNextPreOrder()
        {
            while (true) {
                if (curNode == null)
                {
                    curNode = _root;
                    prevNode = _root;
                    curDepth = 0;
                    return true;
                }
                if (curNode.Left == null && curNode.Right == null)
                {
                    if (curNode.Parent == null)
                    {
                        return false;
                    }
                    prevNode = curNode;
                    curNode = curNode.Parent;
                    curDepth--;
                    continue;
                }
                else if (curNode.Left == null)
                {
                    if (curNode.Right == prevNode)
                    {
                        if (curNode.Parent == null) return false;
                        prevNode = curNode;
                        curNode = curNode.Parent;
                        curDepth--;
                        continue;
                    }
                    prevNode = curNode;
                    curNode = curNode.Right;
                    curDepth++;
                    return true;
                }
                else if (curNode.Right == null)
                {
                    if (curNode.Left == prevNode)
                    {
                        if (curNode.Parent == null) return false;
                        prevNode = curNode;
                        curNode = curNode.Parent;
                        curDepth--;
                        continue;
                    }
                    prevNode = curNode;
                    curNode = curNode.Left;
                    curDepth++;
                    return true;
                }
                else
                {
                    if (curNode.Left == prevNode)
                    {
                        prevNode = curNode;
                        curNode = curNode.Right;
                        curDepth++;
                        return true;
                    }
                    if (curNode.Right == prevNode)
                    {
                        if (curNode.Parent == null) return false;
                        prevNode = curNode;
                        curNode = curNode.Parent;
                        curDepth--;
                        continue;
                    }
                    prevNode = curNode;
                    curNode = curNode.Left;
                    curDepth++;
                    return true;
                }
            }
        }
        private bool MoveNextPreOrderReverse()
        {
            while (true)
            {
                if (curNode == null)
                {
                    curNode = _root;
                    prevNode = _root;
                    curDepth = 0;
                    return true;
                }
                if (curNode.Left == null && curNode.Right == null)
                {
                    if (curNode.Parent == null)
                    {
                        return false;
                    }
                    prevNode = curNode;
                    curNode = curNode.Parent;
                    curDepth--;
                    continue;
                }
                else if (curNode.Right == null)
                {
                    if (curNode.Left == prevNode)
                    {
                        if (curNode.Parent == null) return false;
                        prevNode = curNode;
                        curNode = curNode.Parent;
                        curDepth--;
                        continue;
                    }
                    prevNode = curNode;
                    curNode = curNode.Left;
                    curDepth++;
                    return true;
                }
                else if (curNode.Left == null)
                {
                    if (curNode.Right == prevNode)
                    {
                        if (curNode.Parent == null) return false;
                        prevNode = curNode;
                        curNode = curNode.Parent;
                        curDepth--;
                        continue;
                    }
                    prevNode = curNode;
                    curNode = curNode.Right;
                    curDepth++;
                    return true;
                }
                else
                {
                    if (curNode.Right == prevNode)
                    {
                        prevNode = curNode;
                        curNode = curNode.Left;
                        curDepth++;
                        return true;
                    }
                    if (curNode.Left == prevNode)
                    {
                        if (curNode.Parent == null) return false;
                        prevNode = curNode;
                        curNode = curNode.Parent;
                        curDepth--;
                        continue;
                    }
                    prevNode = curNode;
                    curNode = curNode.Right;
                    curDepth++;
                    return true;
                }
            }
        }
        private bool MoveNextInorder()
        {
            while(true)
            {
                if (curNode == null)
                {
                    curDepth = 0;
                    curNode = MoveFullLeft(_root);
                    prevNode = curNode;
                    return true;
                }
                else if (curNode.Left == null && curNode.Right == null)
                {
                    if (curNode.Parent == null) return false;
                    prevNode = curNode;
                    curNode = curNode.Parent;
                    curDepth--;
                    continue;
                }
                else if (curNode.Left == null)
                {
                    if (prevNode == curNode)
                    {
                        curDepth++;
                        curNode = MoveFullLeft(curNode.Right);
                        prevNode = curNode;
                        return true;
                    }
                    if (prevNode == curNode.Right)
                    {
                        if (curNode.Parent == null) return false;
                        prevNode = curNode;
                        curNode = curNode.Parent;
                        curDepth--;
                        continue;
                    }
                    prevNode = curNode;
                    return true;
                }
                else if (curNode.Right == null)
                {
                    if (prevNode == curNode.Left)
                    {
                        prevNode = curNode;
                        return true;
                    }
                    else if (prevNode == curNode)
                    {
                        if (curNode.Parent == null) return false;
                        prevNode = curNode;
                        curNode = curNode.Parent;
                        curDepth--;
                        continue;
                    }
                    throw new Exception("impossible");
                }
                else
                {
                    if (prevNode == curNode.Left)
                    {
                        prevNode = curNode;
                        return true;
                    }
                    else if (prevNode == curNode.Right)
                    {
                        if (curNode.Parent == null) return false;
                        prevNode = curNode;
                        curNode = curNode.Parent;
                        curDepth--;
                        continue;
                    }
                    else if (prevNode == curNode)
                    {
                        curDepth++;
                        curNode = MoveFullLeft(curNode.Right);
                        prevNode = curNode;
                        return true;
                    }
                    else
                    {
                        throw new Exception("impossible");
                    }
                }
            }

        }
        private bool MoveNextInorderReverse()
        {
            while(true)
            {
                if (curNode == null)
                {
                    curDepth = 0;
                    curNode = MoveFullRight(_root);
                    prevNode = curNode;
                    return true;
                }
                else if (curNode.Left == null && curNode.Right == null)
                {
                    if (curNode.Parent == null) return false;
                    prevNode = curNode;
                    curNode = curNode.Parent;
                    curDepth--;
                    continue;
                }
                else if (curNode.Right == null)
                {
                    if (prevNode == curNode)
                    {
                        curDepth++;
                        curNode = MoveFullRight(curNode.Left);
                        prevNode = curNode;
                        return true;
                    }
                    if (prevNode == curNode.Left)
                    {
                        if (curNode.Parent == null) return false;
                        prevNode = curNode;
                        curNode = curNode.Parent;
                        curDepth--;
                        continue;
                    }
                    prevNode = curNode;
                    return true;
                }
                else if (curNode.Left == null)
                {
                    if (prevNode == curNode.Right)
                    {
                        prevNode = curNode;
                        return true;
                    }
                    else if (prevNode == curNode)
                    {
                        if (curNode.Parent == null) return false;
                        prevNode = curNode;
                        curNode = curNode.Parent;
                        curDepth--;
                        continue;
                    }
                    throw new Exception("impossible");
                }
                else
                {
                    if (prevNode == curNode.Right)
                    {
                        prevNode = curNode;
                        return true;
                    }
                    else if (prevNode == curNode.Left)
                    {
                        if (curNode.Parent == null) return false;
                        prevNode = curNode;
                        curNode = curNode.Parent;
                        curDepth--;
                        continue;
                    }
                    else if (prevNode == curNode)
                    {
                        curDepth++;
                        curNode = MoveFullRight(curNode.Left);
                        prevNode = curNode;
                        return true;
                    }
                    else
                    {
                        throw new Exception("impossible");
                    }
                }
            }
        }
        private bool MoveNextPostOrder(bool reverse = false)
        {
            while(true)
            {
                if (curNode == null)
                {
                    curDepth = 0;
                    curNode = MoveFullLeftRight(_root, reverse);
                    prevNode = curNode;
                    return true;
                }
                if (prevNode == curNode)
                {
                    if (curNode.Parent == null) return false;
                    curNode = curNode.Parent;
                    curDepth--;
                    continue;
                }
                else if (curNode.Left == null)
                {
                    if (prevNode == curNode.Right)
                    {
                        prevNode = curNode;
                        return true;
                    }
                }
                else if (curNode.Right == null)
                {
                    if (prevNode == curNode.Left)
                    {
                        prevNode = curNode;
                        return true;
                    }
                }
                else
                {
                    if (prevNode == curNode.Left)
                    {
                        if (reverse)
                        {
                            prevNode = curNode;
                            return true;
                        }
                        curDepth++;
                        curNode = MoveFullLeftRight(curNode.Right, reverse);
                        prevNode = curNode;
                        return true;
                    }
                    else if (prevNode == curNode.Right)
                    {
                        if (reverse)
                        {
                            curDepth++;
                            curNode = MoveFullLeftRight(curNode.Left, reverse);
                            prevNode = curNode;
                            return true;
                        }
                        prevNode = curNode;
                        return true;
                    }
                }
                throw new Exception("impossible postOrder situation");
            }
        }
        public bool MoveNext()
        {
            bool success = false;
            if (_strategy == TraversalStrategy.PreOrder)
            {
                success = MoveNextPreOrder();
            }
            else if (_strategy == TraversalStrategy.PreOrderReverse)
            {
                success = MoveNextPreOrderReverse();
            }
            else if (_strategy == TraversalStrategy.InOrder)
            {
                success = MoveNextInorder();
            }
            else if (_strategy == TraversalStrategy.InOrderReverse)
            {
                success = MoveNextInorderReverse();
            }
            else if (_strategy == TraversalStrategy.PostOrder)
            {
                success = MoveNextPostOrder();
            }
            else if (_strategy == TraversalStrategy.PostOrderReverse)
            {
                success = MoveNextPostOrder(reverse: true);
            }
            if (!success) return false;
            _current = new TreeEntry<TKey, TValue>(curNode!.Key, curNode.Value, curDepth);
            return success;
        }
        
        public void Reset()
        {
            this.prevNode = null;
            this.curNode = null;
            this.curDepth = 0;
        }


        public void Dispose() { }
    }

    private enum TraversalStrategy { InOrder, PreOrder, PostOrder, InOrderReverse, PreOrderReverse, PostOrderReverse}
    
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        throw new NotImplementedException();
    }
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
    public void Clear() { Root = null; Count = 0; }
    public bool Contains(KeyValuePair<TKey, TValue> item) => ContainsKey(item.Key);
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (arrayIndex + this.Count >= array.Length) throw new ArgumentOutOfRangeException();
        TreeIterator iterator = new TreeIterator();
        for (int i = 0; i < this.Count; i++)
        {
            TreeEntry<TKey, TValue> entry = iterator.Current;
            array[arrayIndex + i] = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
            iterator.MoveNext();
        }
    }
    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);
}