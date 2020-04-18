using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Acs0;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.SmartContract.Infrastructure;
using AElf.Types;
using Google.Protobuf;
using Volo.Abp.DependencyInjection;

namespace AElf.Kernel.SmartContract.Application
{
    public interface ISmartContractAddressService
    {
        Task<Address> GetAddressByContractNameAsync(IChainContext chainContext, Hash name);
        Task<SmartContractAddressDto> GetSmartContractAddressAsync(IChainContext chainContext, Hash name);
        Task SetSmartContractAddressAsync(IBlockIndex blockIndex, Hash contractName, Address address);
        
        void SetAddress(Hash name, Address address);

        Address GetZeroSmartContractAddress();

        Address GetZeroSmartContractAddress(int chainId);

        Task<IReadOnlyDictionary<Hash, Address>> GetSystemContractNameToAddressMappingAsync(IChainContext chainContext);
    }

    public class SmartContractAddressService : ISmartContractAddressService, ISingletonDependency
    {
        private readonly IDefaultContractZeroCodeProvider _defaultContractZeroCodeProvider;
        private readonly ITransactionReadOnlyExecutionService _transactionReadOnlyExecutionService;
        private readonly ISmartContractAddressProvider _smartContractAddressProvider;
        private readonly IEnumerable<ISmartContractAddressNameProvider> _smartContractAddressNameProviders;
        private readonly IBlockchainService _blockchainService;

        public SmartContractAddressService(IDefaultContractZeroCodeProvider defaultContractZeroCodeProvider, 
            ITransactionReadOnlyExecutionService transactionReadOnlyExecutionService, 
            ISmartContractAddressProvider smartContractAddressProvider,
            IEnumerable<ISmartContractAddressNameProvider> smartContractAddressNameProviders, 
            IBlockchainService blockchainService)
        {
            _defaultContractZeroCodeProvider = defaultContractZeroCodeProvider;
            _transactionReadOnlyExecutionService = transactionReadOnlyExecutionService;
            _smartContractAddressProvider = smartContractAddressProvider;
            _smartContractAddressNameProviders = smartContractAddressNameProviders;
            _blockchainService = blockchainService;
        }
        
        private readonly ConcurrentDictionary<Hash, Address> _hashToAddressMap =
            new ConcurrentDictionary<Hash, Address>();

        public Task<Address> GetAddressByContractNameAsync(IChainContext chainContext, Hash name)
        {
            _hashToAddressMap.TryGetValue(name, out var address);
            return Task.FromResult(address);
        }

        public async Task<SmartContractAddressDto> GetSmartContractAddressAsync(IChainContext chainContext, Hash name)
        {
            var smartContractAddress = await _smartContractAddressProvider.GetSmartContractAddressAsync(chainContext, name);
            if (smartContractAddress != null)
            {
                var smartContractAddressDto = new SmartContractAddressDto
                {
                    SmartContractAddress = smartContractAddress,
                    Irreversible = await CheckSmartContractAddressIrreversibleAsync(smartContractAddress)
                };
                
                return smartContractAddressDto;
            }
            var address = await GetSmartContractAddressFromStateAsync(chainContext, name);
            if (address == null) return null;
            return new SmartContractAddressDto
            {
                SmartContractAddress = new SmartContractAddress
                {
                    Address = address
                }
            };
        }
        
        private async Task<bool> CheckSmartContractAddressIrreversibleAsync(SmartContractAddress smartContractAddress)
        {
            var chain = await _blockchainService.GetChainAsync();
            if (smartContractAddress.BlockHeight > chain.LastIrreversibleBlockHeight) return false;

            var blockHash = await _blockchainService.GetBlockHashByHeightAsync(chain,
                smartContractAddress.BlockHeight, chain.LastIrreversibleBlockHash);
            return blockHash == smartContractAddress.BlockHash;
        }

        public virtual async Task SetSmartContractAddressAsync(IBlockIndex blockIndex, Hash contractName, Address address)
        {
            await _smartContractAddressProvider.SetSmartContractAddressAsync(blockIndex, contractName, address);
        }

        public void SetAddress(Hash name, Address address)
        {
            _hashToAddressMap.TryAdd(name, address);
        }

        public Address GetZeroSmartContractAddress()
        {
            return _defaultContractZeroCodeProvider.ContractZeroAddress;
        }

        public Address GetZeroSmartContractAddress(int chainId)
        {
            return _defaultContractZeroCodeProvider.GetZeroSmartContractAddress(chainId);
        }

        public virtual async Task<IReadOnlyDictionary<Hash, Address>> GetSystemContractNameToAddressMappingAsync(
            IChainContext chainContext)
        {
            var map = new Dictionary<Hash,Address>();
            foreach (var smartContractAddressNameProvider in _smartContractAddressNameProviders)
            {
                var address =
                    await GetAddressByContractNameAsync(chainContext, smartContractAddressNameProvider.ContractName);
                if(address != null)
                    map[smartContractAddressNameProvider.ContractName] = address;
            }
            return new ReadOnlyDictionary<Hash, Address>(map);
        }
        
        private async Task<Address> GetSmartContractAddressFromStateAsync(IChainContext chainContext,Hash name)
        {
            var zeroAddress = _defaultContractZeroCodeProvider.ContractZeroAddress;
            var tx = new Transaction
            {
                From = zeroAddress,
                To = zeroAddress,
                MethodName = nameof(ACS0Container.ACS0Stub.GetContractAddressByName),
                Params = name.ToByteString()
            };
            var address = await _transactionReadOnlyExecutionService.ExecuteAsync<Address>(
                chainContext, tx, TimestampHelper.GetUtcNow(), false);

            return address == null || address.Value.IsEmpty ? null : address;
        } 
    }
    
    public class SmartContractAddressDto
    {
        public SmartContractAddress SmartContractAddress { get; set; }
        public bool Irreversible { get; set; }
    }
}