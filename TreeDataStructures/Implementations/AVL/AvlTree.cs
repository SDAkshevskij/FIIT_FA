using System.Runtime.CompilerServices;
using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.AVL;

public class AvlTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, AvlNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override AvlNode<TKey, TValue> CreateNode(TKey key, TValue value)
        => new(key, value);
    
    protected override void OnNodeAdded(AvlNode<TKey, TValue> newNode)
    {
        AvlNode<TKey, TValue>? curNode = newNode.Parent;

        while(curNode != null)
        {
            MakeBalance(curNode);
            curNode = curNode.Parent;
        }
    }
    protected override void OnNodeRemoved(AvlNode<TKey, TValue>? parent, AvlNode<TKey, TValue>? child)
    {
        AvlNode<TKey, TValue>? curNode = parent ?? child;
        while (curNode != null)
        {
            MakeBalance(curNode);
            curNode = curNode.Parent;
        }
    }

    
    private void MakeBalance(AvlNode<TKey, TValue> node)
    {
        int disbalance = GetDisbalance(node);
        if (Math.Abs(disbalance) > 1)
        {
            if (disbalance > 1 && GetDisbalance(node!.Left) >= 0) RotateRight(node);
            else if (disbalance < -1 && GetDisbalance(node.Right) <= 0) RotateLeft(node);
            else if (disbalance > 1 && GetDisbalance(node!.Left) < 0) RotateBigRight(node);
            else if (disbalance < -1 && GetDisbalance(node!.Right) > 0) RotateBigLeft(node);
            else
            {
               // throw new Exception("Unknow AVL balance situation");
            }
        }
        node.Height = CountHeight(node);
    }
    private int GetDisbalance(AvlNode<TKey, TValue> node)
    {
        int leftH = node.Left == null ? 0 : node.Left.Height;
        int rightH = node.Right == null ? 0 : node.Right.Height;
        return leftH - rightH;
    }
    private int CountHeight(AvlNode<TKey, TValue> node)
    {
        int leftH = node.Left == null ? 0 : node.Left.Height;
        int rightH = node.Right == null ? 0 : node.Right.Height;
        return Math.Max(leftH, rightH) + 1;
    }

    
}