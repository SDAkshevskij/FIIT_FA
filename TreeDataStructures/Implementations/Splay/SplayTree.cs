using System.Diagnostics.CodeAnalysis;
using System.Net.NetworkInformation;
using System.Xml.XPath;
using TreeDataStructures.Implementations.BST;

namespace TreeDataStructures.Implementations.Splay;

public class SplayTree<TKey, TValue> : BinarySearchTree<TKey, TValue>
    where TKey : IComparable<TKey>
{
    protected override BstNode<TKey, TValue> CreateNode(TKey key, TValue value)
        => new(key, value);
    
    protected override void OnNodeAdded(BstNode<TKey, TValue> newNode)
    {
        Splay(newNode);
    }
    protected override void RemoveNode(BstNode<TKey, TValue> node)
    {
        if (node == null) return;
        Splay(node);
        BstNode<TKey, TValue>? leftTree = node.Left;
        BstNode<TKey, TValue>? rightTree = node.Right;
        leftTree?.Parent = null;
        rightTree?.Parent = null;
        if (leftTree == null)
        {
            Root = rightTree;
            return;
        }
        else if (rightTree == null)
        {
            Root = leftTree;
            return;
        }
        BstNode<TKey, TValue> smallestRight = GetSmallestSubtreeNode(rightTree);
        Splay(smallestRight);
        smallestRight.Left = leftTree;
        leftTree.Parent = smallestRight;
        Root = smallestRight;
    }
    protected override void OnNodeRemoved(BstNode<TKey, TValue>? parent, BstNode<TKey, TValue>? child){}
    
    public override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        BstNode<TKey, TValue>? curNode = Root;
        while (curNode != null)
        {
            int cmp = this.Comparer.Compare(key, curNode.Key);
            if (cmp == 0)
            {
                value = curNode.Value;
                Splay(curNode);
                return true;
            }
            else if (cmp < 0)
            {
                if (curNode.Left == null)
                {
                    Splay(curNode);
                    value = default;
                    return false;
                }
                curNode = curNode.Left;
            }
            else
            {
                if ( curNode.Right == null)
                {
                    Splay(curNode);
                    value = default;
                    return false;
                }
                curNode = curNode.Right;
            }
        }
        value = default;
        return false;
    }
    private void Splay(BstNode<TKey, TValue> node)
    {
        if (node == null) return;
        while(node.Parent != null)
        {
            bool isNodeLeft = node.IsLeftChild;
            BstNode<TKey, TValue> parent = node.Parent;
            BstNode<TKey, TValue>? grand = parent.Parent;
            if (grand == null)
            {
                if (isNodeLeft) RotateRight(parent);
                else RotateLeft(parent);
            }
            else
            {
                bool isParentLeft = parent.IsLeftChild;
                if (isParentLeft && isNodeLeft) RotateDoubleRight(grand);
                else if (isParentLeft && !isNodeLeft) RotateBigRight(grand);
                else if (!isParentLeft && isNodeLeft) RotateBigLeft(grand);
                else RotateDoubleLeft(grand);
            }
        }
        this.Root = node;
    }
    
}
