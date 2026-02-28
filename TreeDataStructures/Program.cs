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
            Treap<int, string> tree = new Treap<int, string>();

            tree.Add(10, "Initial");
            tree[10] = "Updated"; // Тест индексатора set

            foreach (var node in tree.InOrder())
            {
                Console.WriteLine(node);
            }
        }
    }
}