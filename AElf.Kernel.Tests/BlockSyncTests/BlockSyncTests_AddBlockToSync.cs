﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common.ByteArrayHelpers;
using AElf.Kernel.Node;
using AElf.Kernel.Node.Protocol;
using AElf.Kernel.Node.Protocol.Exceptions;
using Castle.DynamicProxy.Generators;
using Google.Protobuf.WellKnownTypes;
using Moq;
using Xunit;

namespace AElf.Kernel.Tests.BlockSyncTests
{
    public class BlockSyncTests_AddBlockToSync
    {
        public byte[] RandomFill(int count)
        {
            Random rnd = new Random();
            byte[] random = new byte[count];
            
            rnd.NextBytes(random);

            return random;
        }
        
        private Block GenerateValidBlockToSync()
        {
            var block = new Block(RandomFill(10));

            block.Header.ChainId = RandomFill(10);
            block.Header.Time = Timestamp.FromDateTime(DateTime.UtcNow);
            block.Header.PreviousBlockHash = RandomFill(256);
            
            return block;
        }

        [Fact]
        public async Task AddBlockToSync_NullBlock_ShouldThrow()
        {
            BlockSynchronizer s = new BlockSynchronizer(null);
            
            Exception ex = await Assert.ThrowsAsync<InvalidBlockException>(() => s.AddBlockToSync(null));
            Assert.Equal("The block, blockheader or body is null", ex.Message);
            
            Exception ex2 = await Assert.ThrowsAsync<InvalidBlockException>(() => s.AddBlockToSync(new Block()));
            Assert.Equal("The block, blockheader or body is null", ex2.Message);
            
            Exception ex3 = await Assert.ThrowsAsync<InvalidBlockException>(() => s.AddBlockToSync(new Block()));
            Assert.Equal("The block, blockheader or body is null", ex3.Message);
        }

        [Fact]
        public async Task AddBlockToSync_NoTransactions_ShouldThrow()
        {
            BlockSynchronizer s = new BlockSynchronizer(null);
            
            Block b = new Block();
            b.Body = new BlockBody();
            b.Header = new BlockHeader();
            
            Exception ex = await Assert.ThrowsAsync<InvalidBlockException>(() => s.AddBlockToSync(b));
            Assert.Equal("The block contains no transactions", ex.Message);
        }

        [Fact]
        public async Task AddBlockToSync_NoHash_ShouldThrow()
        {
            BlockSynchronizer s = new BlockSynchronizer(null);
            
            Block b = new Block();
            b.Body = new BlockBody();
            b.Header = new BlockHeader();
            b.AddTransaction(new Hash());
            
            Exception ex = await Assert.ThrowsAsync<InvalidBlockException>(() => s.AddBlockToSync(b));
            Assert.Equal("Invalid block hash", ex.Message);
        }

        [Fact]
        public async Task AddBlockToSync_TxMissing_ShouldPutBlockToSync()
        {
            var missingTxHash = RandomFill(256);
            var returnTxHashes = new List<Hash> { new Hash(missingTxHash) };
            
            Mock<IAElfNode> mock = new Mock<IAElfNode>();
            mock.Setup(n => n.GetMissingTransactions(It.IsAny<IBlock>())).Returns(returnTxHashes);
            
            IAElfNode m = mock.Object;
            
            BlockSynchronizer s = new BlockSynchronizer(m);

            Block b = GenerateValidBlockToSync();
            b.AddTransaction(Hash.Generate());
            
            await s.AddBlockToSync(b);

            byte[] array = b.GetHash().GetHashBytes();
            PendingBlock p = s.GetBlock(array);
            
            Assert.Equal(p.BlockHash, array);

            byte[] missingTx = p.MissingTxs.FirstOrDefault();
            Assert.True(missingTx.BytesEqual(missingTxHash));
        }

        [Fact]
        public async Task AddBlockToSync_AlreadyInPool_ShouldPutBlockToSyncIfOrphan()
        {
            Mock<IAElfNode> mock = new Mock<IAElfNode>();
            mock.Setup(n => n.GetMissingTransactions(It.IsAny<IBlock>())).Returns<List<Hash>>(null);
        }

        /*[Fact]
        public async Task AddBlockToSync_TxMissing_ShouldPutBlockToSync()
        {
            Mock<IAElfNode> mock = new Mock<IAElfNode>();
            
            mock.Setup(n => n.GetMissingTransactions(It.IsAny<IBlock>()))
                .Returns(new List<Hash> { new Hash(RandomFill(256)) });
            
            IAElfNode m = mock.Object;
            
            BlockSynchronizer s = new BlockSynchronizer(m);

            Block b = GenerateValidBlockToSync();
            await s.AddBlockToSync(b);

            byte[] array = b.GetHash().GetHashBytes();
            PendingBlock p = s.GetBlock(array);
            
            Assert.Equal(p.BlockHash, array);
        }*/
        
        /*[Fact]
        public void AddBlock_AllTxInPool_ShouldFireBlockSynched()
        {
            Mock<IAElfNode> mock = new Mock<IAElfNode>();
            mock.Setup(n => n.GetMissingTransactions(It.IsAny<IBlock>())).Returns(new List<Hash>());
            IAElfNode m = mock.Object;
            
            BlockSynchronizer s = new BlockSynchronizer(m);
            
            List<BlockSynchedArgs> receivedEvents = new List<BlockSynchedArgs>();
            
            s.BlockSynched += (sender, e) =>
            {
                BlockSynchedArgs args = e as BlockSynchedArgs;
                receivedEvents.Add(args);
            };
            
            Block b = new Block();
            s.AddBlockToSync(b);
            
            Assert.Equal(1, receivedEvents.Count);
            Assert.Equal(receivedEvents[0].Block, b);
        }*/
    }
}