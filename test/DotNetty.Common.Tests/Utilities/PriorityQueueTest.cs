// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Tests.Utilities
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Utilities;
    using Xunit;

    public class PriorityQueueTest
    {
        [Theory]
        [InlineData(0, -1)]
        [InlineData(1, 0)]
        [InlineData(1, -1)]
        [InlineData(2, 0)]
        [InlineData(2, 1)]
        [InlineData(3, 0)]
        [InlineData(3, 1)]
        [InlineData(3, 2)]
        [InlineData(7, 5)]
        public void PriorityQueueRemoveTest(int length, int removeIndex)
        {
            var queue = new PriorityQueue<TestNode>();
            for (int i = length - 1; i >= 0; i--)
            {
                queue.TryEnqueue(new TestNode(i));
            }

            if (removeIndex == -1)
            {
                queue.TryRemove(new TestNode(length));
                Assert.Equal(length, queue.Count);
            }
            else
            {
                queue.TryRemove(new TestNode(removeIndex));
                Assert.Equal(length - 1, queue.Count);
            }
        }

        [Theory]
        [InlineData(new[] { 1, 2, 3, 4 }, new[] { 1, 2, 3, 4 })]
        [InlineData(new[] { 4, 3, 2, 1 }, new[] { 1, 2, 3, 4 })]
        [InlineData(new[] { 3, 2, 1 }, new[] { 1, 2, 3 })]
        [InlineData(new[] { 1, 3, 2 }, new[] { 1, 2, 3 })]
        [InlineData(new[] { 1, 2 }, new[] { 1, 2 })]
        [InlineData(new[] { 2, 1 }, new[] { 1, 2 })]
        public void PriorityQueueOrderTest(int[] input, int[] expectedOutput)
        {
            var queue = new PriorityQueue<TestNode>();
            foreach (int value in input)
            {
                queue.TryEnqueue(new TestNode(value));
            }

            for (int index = 0; index < expectedOutput.Length; index++)
            {
                Tuple<int> item = queue.Dequeue();
                Assert.Equal(expectedOutput[index], item.Item1);
            }
            Assert.Equal(0, queue.Count);
        }

        class TestNode : Tuple<int>, IPriorityQueueNode<TestNode>
        {
            int queueIndex;
            
            public TestNode(int item1)
                : base(item1)
            {
            }

            public int GetPriorityQueueIndex(PriorityQueue<TestNode> queue) => this.queueIndex;

            public void SetPriorityQueueIndex(PriorityQueue<TestNode> queue, int i) => this.queueIndex = i;

        }
        
    }
}