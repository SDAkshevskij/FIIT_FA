using System;
using TreeDataStructures.Core;
using TreeDataStructures.Implementations.AVL;
using TreeDataStructures.Implementations.BST;
using TreeDataStructures.Implementations.Splay;
using TreeDataStructures.Interfaces;
using TreeDataStructures.Implementations.Treap;

namespace YourTestNamespace
{
    class Program
    {
        static void Main()
        {
            BinarySearchTree<int, string> tree = new BinarySearchTree<int, string>();

            tree.Add(10, "Root");
            tree.Add(5, "Left");
            tree.Add(15, "Right");
            tree.Add(3, "LeftLeft");
            tree.Add(12, "RightLeft");
            tree.Add(13, "RightLeft");
            tree.Add(14, "RightLeft");

            foreach (var node in tree.PostOrder())
            {
                Console.WriteLine(node.ToString());
            }
        }
    }
}