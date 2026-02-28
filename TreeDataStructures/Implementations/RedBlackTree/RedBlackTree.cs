using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.RedBlackTree;

public class RedBlackTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, RbNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override RbNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        return new RbNode<TKey, TValue>(key, value); 
    }
    
    protected override void OnNodeAdded(RbNode<TKey, TValue> newNode)
    {
        RbNode<TKey, TValue> curNode = newNode;
        while (curNode != Root && curNode.Parent.Color == RbColor.Red)
        {
            RbNode<TKey, TValue> parent = curNode.Parent;
            RbNode<TKey, TValue> grand = parent!.Parent;
            RbNode<TKey, TValue>? uncle = grand.IsLeftChild ? grand.Right: grand.Left;
            if (uncle != null && uncle.Color == RbColor.Red)
            {
                parent.Color = RbColor.Black;
                uncle.Color = RbColor.Black;
                grand.Color = RbColor.Red;
                curNode = grand;
            }
            else
            {
                bool isNodeLeft = curNode.IsLeftChild;
                bool isParentLeft = parent.IsLeftChild;
                if (isNodeLeft && isParentLeft)
                {
                    RotateRight(grand);
                    parent.Color = RbColor.Black;
                    grand.Color = RbColor.Red;
                }
                else if (isParentLeft && !isNodeLeft)
                {
                    RotateBigRight(grand);
                    curNode.Color = RbColor.Black;
                    grand.Color = RbColor.Red;
                }
                else if (!isParentLeft && !isNodeLeft)
                {
                    RotateLeft(grand);
                    parent.Color = RbColor.Black;
                    grand.Color = RbColor.Red;
                }
                else
                {
                    RotateBigLeft(grand);
                    curNode.Color = RbColor.Black;
                    grand.Color = RbColor.Red; 
                }
                break;
            }
        }
        Root?.Color = RbColor.Black;
    }
    protected override void OnNodeRemoved(RbNode<TKey, TValue>? nodeParent, RbNode<TKey, TValue>? nodeChild)
    {
        RbNode<TKey, TValue>? curNode = nodeChild ?? nodeParent;
        if (curNode == null) return;
        while (curNode.Color == RbColor.Black && curNode != Root)
        {
            RbNode<TKey, TValue> parent = curNode.Parent;
            bool isNodeLeft = curNode.IsLeftChild;
            RbNode<TKey, TValue> sibling = isNodeLeft ? parent.Right : parent.Left;

            if (sibling == null)
            {
                curNode = parent;
                continue;
            }

            if (IsRed(sibling))
            {
                parent.Color = RbColor.Red;
                sibling.Color = RbColor.Black;
                if (isNodeLeft)
                {
                    RotateLeft(parent);
                }
                else
                {
                    RotateRight(parent);
                }
                continue;
            }
            RbNode<TKey, TValue> farKid = isNodeLeft ? sibling.Right : sibling.Left;
            RbNode<TKey, TValue> closeKid = isNodeLeft ? sibling.Left : sibling.Right;

            if (IsBlack(sibling.Left) && IsBlack(sibling.Right))
            {
                sibling.Color = RbColor.Red;
                curNode = parent;
            }
            else if (IsRed(farKid))
            {
                if (isNodeLeft) RotateLeft(parent);
                else RotateRight(parent);
                sibling.Color = parent.Color;
                parent.Color = RbColor.Black;
                farKid.Color = RbColor.Black;
                break;
            }
            else if (IsRed(closeKid) && IsBlack(farKid))
            {
                if (isNodeLeft) RotateRight(sibling);
                else RotateLeft(sibling);
                sibling.Color = RbColor.Red;
                closeKid.Color = RbColor.Black;
                sibling = closeKid;
                continue;
            }
        }
        if (curNode != null) curNode.Color = RbColor.Black;
    }

    private bool IsBlack(RbNode<TKey, TValue>? node)
    {
        return node == null || node.Color == RbColor.Black;
    }
    private bool IsRed(RbNode<TKey, TValue>? node) {
        return node != null && node.Color == RbColor.Red;
    }
}