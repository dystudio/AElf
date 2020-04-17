using System.Threading.Tasks;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;
using AElf.Contracts.Configuration;
using AElf.CSharp.Core.Extension;
using AElf.Kernel.Blockchain.Application;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.Threading;

namespace AElf.Kernel.Configuration
{
    public class BlockTransactionLimitChangedLogEventProcessor : LogEventProcessorSpecialBase, IBlockAcceptedLogEventProcessor
    {
        private readonly IBlockTransactionLimitProvider _blockTransactionLimitProvider;
        private readonly ISmartContractAddressService _smartContractAddressService;
        private readonly IBlockchainService _blockchainService;

        public ILogger<BlockTransactionLimitChangedLogEventProcessor> Logger { get; set; }

        public BlockTransactionLimitChangedLogEventProcessor(ISmartContractAddressService smartContractAddressService,
            IBlockTransactionLimitProvider blockTransactionLimitProvider, IBlockchainService blockchainService)
        {
            _smartContractAddressService = smartContractAddressService;
            _blockTransactionLimitProvider = blockTransactionLimitProvider;
            _blockchainService = blockchainService;
            Logger = NullLogger<BlockTransactionLimitChangedLogEventProcessor>.Instance;
        }
        
        public override async Task<InterestedEvent> GetInterestedEventAsync(IChainContext chainContext)
        {
            if (InterestedEvent != null)
                return InterestedEvent;

            var smartContractAddressDto = await _smartContractAddressService.GetSmartContractAddressAsync(
                chainContext, ConfigurationSmartContractAddressNameProvider.Name);
            
            if (smartContractAddressDto == null) return null;
            
            var interestedEvent =
                GetInterestedEvent<ConfigurationSet>(smartContractAddressDto.SmartContractAddress.Address);
            
            if (!smartContractAddressDto.Irreversible)return interestedEvent;
            
            InterestedEvent = interestedEvent;

            return InterestedEvent;
        }

        protected override async Task ProcessLogEventAsync(Block block, LogEvent logEvent)
        {
            var configurationSet = new ConfigurationSet();
            configurationSet.MergeFrom(logEvent);

            if (configurationSet.Key != BlockTransactionLimitConfigurationNameProvider.Name) return;

            var limit = new Int32Value();
            limit.MergeFrom(configurationSet.Value.ToByteArray());
            if (limit.Value < 0) return;
            await _blockTransactionLimitProvider.SetLimitAsync(new BlockIndex
            {
                BlockHash = block.GetHash(),
                BlockHeight = block.Height
            }, limit.Value);

            Logger.LogInformation($"BlockTransactionLimit has been changed to {limit.Value}");
        }
    }
}