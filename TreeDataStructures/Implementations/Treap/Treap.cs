using System.Diagnostics.CodeAnalysis;
using System.Net.NetworkInformation;
using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.Treap;

public class Treap<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, TreapNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    /// <summary>
    /// Разрезает дерево с корнем <paramref name="root"/> на два поддерева:
    /// Left: все ключи <= <paramref name="key"/>
    /// Right: все ключи > <paramref name="key"/>
    /// </summary>
    protected virtual (TreapNode<TKey, TValue>? Left, TreapNode<TKey, TValue>? Right)
        Split(TreapNode<TKey, TValue>? root, TKey key, bool includeEqual = false)
    {
        if (root == null) return (null, null);
        int cmp = Comparer.Compare(key, root.Key);
        if (cmp > 0 || (includeEqual && cmp == 0))
        {
            var (l, r) = Split(root.Right, key, includeEqual);
            root.Right = l;
            l?.Parent = root;
            r?.Parent = null;
            root.Parent = null;
            return (root, r);
        }
        else
        {
            var (l, r) = Split(root.Left, key, includeEqual);
            root.Left = r;
            r?.Parent = root;
            l?.Parent = null;
            root.Parent = null;
            return (l, root);
        }
    }

    /// <summary>
    /// Сливает два дерева в одно.
    /// Важное условие: все ключи в <paramref name="left"/> должны быть меньше ключей в <paramref name="right"/>.
    /// Слияние происходит на основе Priority (куча).
    /// </summary>
    protected virtual TreapNode<TKey, TValue>? Merge(TreapNode<TKey, TValue>? left, TreapNode<TKey, TValue>? right)
    {
        if (left == null) return right;
        if (right == null) return left;

        if (left.Priority > right.Priority)
        {
            left.Right = Merge(left.Right, right);
            left.Right?.Parent = left;
            return left;
        }
        else
        {
            right.Left = Merge(left, right.Left);
            right.Left?.Parent = right;
            return right;
        }
    }
    

    public override void Add(TKey key, TValue value)
    {
        var trees = Split(this.Root, key, includeEqual: false);
        (var target, var right) = Split(trees.Right, key, includeEqual: true);
        if (target != null)
        {
            this.Root = Merge(trees.Left, target);
            this.Root = Merge(this.Root, right);
            return;
        }
        TreapNode<TKey, TValue> node = CreateNode(key, value);
        var leftTree = Merge(trees.Left, node);
        var fullTree = Merge(leftTree, trees.Right);
        this.Root = fullTree;
    }

    public override bool Remove(TKey key)
    {
        var trees = Split(this.Root, key, includeEqual: false);
        (var target, var right) = Split(trees.Right, key, includeEqual: true);
        if (target == null) return false;
        this.Root = Merge(trees.Left, right);
        return true;
    }

    protected override TreapNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        return new TreapNode<TKey, TValue>(key, value);
    }
    protected override void OnNodeAdded(TreapNode<TKey, TValue> newNode)
    {
        return;
    }
    
    protected override void OnNodeRemoved(TreapNode<TKey, TValue>? parent, TreapNode<TKey, TValue>? child)
    {
        return;
    }
    
}