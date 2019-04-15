using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AuspiciousCranes.Multiplayer
{
    public class Matchmaker_Backup
    {
        internal class MatchmakerProcess
        {
            public MatchmakerProcess(string id, float rating)
            {
                Id = id;
                Rating = rating;
                upper = Rating;
                lower = Rating;
            }

            public string Id { get; }
            public float Rating;
            public LinkedListNode<MatchmakerProcess> Node;
            private int currentCount;
            private float upper;
            private float lower;

            public bool TryLinearEnlarge(int maxCount, float deltaOffset, float? maxOffset = default)
            {
                if (currentCount >= maxCount)
                {
                    return false;
                }
                var offset = deltaOffset * currentCount;
                var clampedOffset = maxOffset.HasValue ? Math.Min(offset, maxOffset.Value) : offset;
                upper = Rating + clampedOffset;
                lower = Rating - clampedOffset;
                currentCount += 1;
                return true;
            }

            public bool Overlaps(in MatchmakerProcess other)
            {
                return Id != other.Id && lower <= other.upper && upper >= other.lower;
            }

            public override string ToString()
            {
                return $"<{Id}, {Rating} ({lower}, {upper})>";
            }
        }


        private readonly int _requiredCount;
        private readonly ILogger _logger;
        private readonly LinkedList<MatchmakerProcess> _processes = new LinkedList<MatchmakerProcess>();

        public Matchmaker_Backup(string id, int requiredCount, ILoggerFactory loggerFactory)
        {
            if (requiredCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredCount));
            }
            Id = id;
            _requiredCount = requiredCount;
            _logger = loggerFactory.CreateLogger<Matchmaker_Backup>();
        }

        public string Id { get; }

        public IEnumerable<IEnumerable<string>> MakeMatch()
        {
            var result = new List<List<LinkedListNode<MatchmakerProcess>>>();

            var self = _processes.First;
            while (self != default)
            {
                var nodesOverlaps = new List<LinkedListNode<MatchmakerProcess>> { self };

                var other = _processes.First;
                while (other != default && nodesOverlaps.Count < _requiredCount) // 列表达到需要的数量？
                {
                    if (nodesOverlaps.All(x => x.Value.Overlaps(other.Value))) // 该元素与当前列表中所有项交叠？
                    {
                        nodesOverlaps.Add(other);
                    }
                    other = other.Next;
                }

                if (nodesOverlaps.Count < _requiredCount) // 遍历完成，列表达到需要的数量？
                {
                    self = self.Next;
                    continue;
                }

                result.Add(nodesOverlaps);
                foreach (var nodeOverlaps in nodesOverlaps) // 从总表中移除，下次遍历将不再处理这些项
                {
                    if (nodeOverlaps == self) // 确保下一项未被移除
                    {
                        self = self.Next;
                    }
                    nodeOverlaps.List.Remove(nodeOverlaps);
                }
            }

            return result.Select(x => x.Select(y => y.Value.Id));
        }

        private long _updateCount = 0;

        public void Update()
        {
            var node = _processes.First;
            while (node != default)
            {
                var nextNode = node.Next;
                if (!node.Value.TryLinearEnlarge(11, 10.0f, 100.0f))
                {
                    node.List.Remove(node);
                }
                node = nextNode;
            }
            _updateCount += 1;
        }

        public bool Add(string id, float rating)
        {
            var node = _processes.First;
            while (node != default)
            {
                var nextNode = node.Next;
                if (node.Value.Id == id)
                {
                    return false;
                }
                node = nextNode;
            }
            var toAdd = new MatchmakerProcess(id, rating);
            var newNode = _processes.AddLast(toAdd);
            toAdd.Node = newNode;
            return true;
        }

        public bool Remove(string id)
        {
            var node = _processes.First;
            while (node != default)
            {
                var nextNode = node.Next;
                if (node.Value.Id == id)
                {
                    node.List.Remove(node);
                    return true;
                }
                node = nextNode;
            }
            return false;
        }

        public bool SetRating(string id, int rating)
        {
            var node = _processes.First;
            while (node != default)
            {
                var nextNode = node.Next;
                if (node.Value.Id == id)
                {
                    node.Value.Rating = rating;
                    return true;
                }
                node = nextNode;
            }
            return false;
        }
    }
}
