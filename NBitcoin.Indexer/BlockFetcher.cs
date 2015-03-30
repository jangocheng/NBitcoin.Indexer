﻿using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public class BlockInfo
    {
        public int Height
        {
            get;
            set;
        }
        public uint256 BlockId
        {
            get;
            set;
        }
        public Block Block
        {
            get;
            set;
        }
    }
    public class BlockFetcher : IEnumerable<BlockInfo>, IDisposable
    {

        private readonly Checkpoint _Checkpoint;
        public Checkpoint Checkpoint
        {
            get
            {
                return _Checkpoint;
            }
        }
        private readonly Node _Node;
        public Node Node
        {
            get
            {
                return _Node;
            }
        }

        private readonly ChainBase _BlockHeaders;
        public ChainBase BlockHeaders
        {
            get
            {
                return _BlockHeaders;
            }
        }
        public BlockFetcher(Checkpoint checkpoint, Node node, ChainBase blockHeaders)
        {
            if (node == null)
                throw new ArgumentNullException("node");
            if (blockHeaders == null)
                throw new ArgumentNullException("blockHeaders");
            if (checkpoint == null)
                throw new ArgumentNullException("checkpoint");
            NeedSaveInterval = TimeSpan.FromMinutes(15);
            _BlockHeaders = blockHeaders;
            _Node = node;
            _Checkpoint = checkpoint;
        }

        public TimeSpan NeedSaveInterval
        {
            get;
            set;
        }


        #region IEnumerable<BlockInfo> Members

        ChainedBlock _LastProcessed;
        public IEnumerator<BlockInfo> GetEnumerator()
        {
            Queue<DateTime> lastLogs = new Queue<DateTime>();
            Queue<int> lastHeights = new Queue<int>();

            var fork = _BlockHeaders.FindFork(_Checkpoint.BlockLocator);
            var headers = _BlockHeaders.EnumerateAfter(fork);
            headers = headers.Where(h => h.Height >= FromHeight && h.Height <= ToHeight);
            var first = headers.FirstOrDefault();
            if (first == null)
                yield break;
            var height = first.Height;
            if (first.Height == 1)
            {
                headers = new[] { fork }.Concat(headers);
                height = 0;
            }

            foreach (var block in _Node.GetBlocks(headers.Select(b => b.HashBlock)))
            {
                var header = _BlockHeaders.GetBlock(height);
                _LastProcessed = header;
                yield return new BlockInfo()
                {
                    Block = block,
                    BlockId = header.HashBlock,
                    Height = header.Height
                };

                IndexerTrace.Processed(height, Math.Min(ToHeight, _BlockHeaders.Tip.Height), lastLogs, lastHeights);
                height++;
            }
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion



        private DateTime _LastSaved = DateTime.UtcNow;
        public bool NeedSave
        {
            get
            {
                return (DateTime.UtcNow - _LastSaved) > NeedSaveInterval;
            }
        }

        public void SaveCheckpoint()
        {
            if (_LastProcessed != null)
            {
                _Checkpoint.SaveProgress(_LastProcessed);
                IndexerTrace.CheckpointSaved(_LastProcessed, _Checkpoint.CheckpointName);
            }
            _LastSaved = DateTime.UtcNow;
        }

        public int FromHeight
        {
            get;
            set;
        }

        public int ToHeight
        {
            get;
            set;
        }

        #region IDisposable Members

        public void Dispose()
        {
            _Node.Dispose();
        }

        #endregion
    }
}
