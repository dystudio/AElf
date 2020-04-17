﻿using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core.Extension;
using AElf.Kernel.FeeCalculation.Extensions;
using AElf.Kernel.FeeCalculation.Infrastructure;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.Token;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.Threading;

namespace AElf.Kernel.FeeCalculation.Application
{
    public class TransactionFeeCalculatorCoefficientUpdatedLogEventProcessor : LogEventProcessorSpecialBase,
        IBlockAcceptedLogEventProcessor
    {
        private readonly ISmartContractAddressService _smartContractAddressService;
        private readonly ICalculateFunctionProvider _calculateFunctionProvider;

        private ILogger<TransactionFeeCalculatorCoefficientUpdatedLogEventProcessor> Logger { get; set; }

        public TransactionFeeCalculatorCoefficientUpdatedLogEventProcessor(
            ISmartContractAddressService smartContractAddressService,
            ICalculateFunctionProvider calculateFunctionProvider)
        {
            _smartContractAddressService = smartContractAddressService;
            _calculateFunctionProvider = calculateFunctionProvider;
            Logger = NullLogger<TransactionFeeCalculatorCoefficientUpdatedLogEventProcessor>.Instance;
        }
        
        public override async Task<InterestedEvent> GetInterestedEventAsync(IChainContext chainContext)
        {
            if (InterestedEvent != null)
                return InterestedEvent;

            var smartContractAddressDto = await _smartContractAddressService.GetSmartContractAddressAsync(
                chainContext, TokenSmartContractAddressNameProvider.Name);
                
            if (smartContractAddressDto == null) return null;
            var interestedEvent =
                GetInterestedEvent<CalculateFeeAlgorithmUpdated>(smartContractAddressDto.SmartContractAddress.Address);
            
            if (!smartContractAddressDto.Irreversible)return interestedEvent;
            
            InterestedEvent = interestedEvent;

            return InterestedEvent;
        }

        protected override async Task ProcessLogEventAsync(Block block, LogEvent logEvent)
        {
            var eventData = new CalculateFeeAlgorithmUpdated();
            eventData.MergeFrom(logEvent);
            await _calculateFunctionProvider.AddCalculateFunctions(new BlockIndex
            {
                BlockHash = block.GetHash(),
                BlockHeight = block.Height
            }, eventData.AllTypeFeeCoefficients.ToCalculateFunctionDictionary());
        }
    }
}