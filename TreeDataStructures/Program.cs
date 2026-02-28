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

            foreach (var node in tree.InOrder())
            {
                Console.WriteLine(node.ToString());
            }
        }
    }
}