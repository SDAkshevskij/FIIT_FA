using System.Collections;
using System.Diagnostics.CodeAnalysis;
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

    public ICollection<TKey> Keys => throw new NotImplementedException();
    public ICollection<TValue> Values => throw new NotImplementedException();
    
    
    public virtual void Add(TKey key, TValue value)
    {
        //throw new NotImplementedException(
        //    "Implement standard BST add logic using <CreateNode(key, value)> and OnNodeAdded(newNode)");
        TNode node = CreateNode(key, value);

        if (Root == null) {
            Root = node;
            OnNodeAdded(node); // maybe useless
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
        //throw new NotImplementedException("Implement standard BST delete logic using Transplant helper");
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
        else if (node.Left != null) // Only left child
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

    public virtual bool ContainsKey(TKey key) => FindNode(key) != null;
    
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

        if (x.IsLeftChild) mainParent?.Left = right;
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
        RotateLeft(x);
        RotateLeft(x);
    }
    
    protected void RotateDoubleRight(TNode y)
    {
        RotateRight(y);
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
    
    public IEnumerable<TreeEntry<TKey, TValue>>  InOrder() => InOrderTraversal(Root);
    
    private IEnumerable<TreeEntry<TKey, TValue>>  InOrderTraversal(TNode? node)
    {
        if (node == null) {  yield break; }
        throw new NotImplementedException();
    }
    
    public IEnumerable<TreeEntry<TKey, TValue>>  PreOrder() => throw new NotImplementedException();
    public IEnumerable<TreeEntry<TKey, TValue>>  PostOrder() => throw new NotImplementedException();
    public IEnumerable<TreeEntry<TKey, TValue>>  InOrderReverse() => throw new NotImplementedException();
    public IEnumerable<TreeEntry<TKey, TValue>>  PreOrderReverse() => throw new NotImplementedException();
    public IEnumerable<TreeEntry<TKey, TValue>>  PostOrderReverse() => throw new NotImplementedException();
    
    /// <summary>
    /// Внутренний класс-итератор. 
    /// Реализует паттерн Iterator вручную, без yield return (ban).
    /// </summary>
    private struct TreeIterator : 
        IEnumerable<TreeEntry<TKey, TValue>>,
        IEnumerator<TreeEntry<TKey, TValue>>
    {
        // probably add something here
        private readonly TraversalStrategy _strategy; // or make it template parameter?
        
        public IEnumerator<TreeEntry<TKey, TValue>> GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => this;
        
        public TreeEntry<TKey, TValue> Current => throw new NotImplementedException();
        object IEnumerator.Current => Current;
        
        
        public bool MoveNext()
        {
            if (_strategy == TraversalStrategy.InOrder)
            {
                throw new NotImplementedException();
            }
            throw new NotImplementedException("Strategy not implemented");
        }
        
        public void Reset()
        {
            throw new NotImplementedException();
        }

        
        public void Dispose()
        {
            // TODO release managed resources here
        }
    }
    
    
    private enum TraversalStrategy { InOrder, PreOrder, PostOrder, InOrderReverse, PreOrderReverse, PostOrderReverse }
    
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        throw new NotImplementedException();
    }
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
    public void Clear() { Root = null; Count = 0; }
    public bool Contains(KeyValuePair<TKey, TValue> item) => ContainsKey(item.Key);
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => throw new NotImplementedException();
    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);
}