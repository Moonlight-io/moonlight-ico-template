using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System.Numerics;

namespace Neo.SmartContract
{
    public class Administration : Framework.SmartContract
    {

        public static string[] GetAdministrationMethods() => new string[] {
            "AllocatePresalePurchase",
            "ContractMigrate",
            "EnableDEXWhitelisting",
            "InitSmartContract",
            "LockPresaleAllocation",
            "UnlockFoundersTokens",
            "UpdateAdminAddress",
            "WhitelistDEXAdd",
            "WhitelistDEXRemove"
        };

        public static object HandleAdministrationOperation(string operation, params object[] args)
        {
            if (operation == "WhitelistDEXRemove")
            {
                if (!Helpers.RequireArgumentLength(args, 2))
                {
                    return false;
                }

                return WhitelistDEXRemove((byte[])args[1]);
            }
            else if (operation == "WhitelistDEXAdd")
            {
                if (!Helpers.RequireArgumentLength(args, 2))
                {
                    return false;
                }
                return WhitelistDEXAdd((byte[])args[1]);
            } else if (operation == "EnableDEXWhitelisting")
            {
                if (!Helpers.RequireArgumentLength(args, 2))
                {
                    return false;
                }
                EnableDEXWhitelisting((bool)args[1]);
            }


            switch (operation)
            {
                case "AllocatePresalePurchase":
                    if (!Helpers.RequireArgumentLength(args, 3))
                    {
                        return false;
                    }
                    return AllocatePresalePurchase((byte[])args[1], (BigInteger)args[2]);
                case "ContractMigrate":
                    if (!Helpers.RequireArgumentLength(args, 10))
                    {
                        return false;
                    }
                    return ContractMigrate(args);
                case "InitSmartContract":
                    return InitSmartContract();
                case "LockPresaleAllocation":
                    return LockPresaleAllocation();
                case "UnlockFoundersTokens":
                    if (!Helpers.RequireArgumentLength(args, 3))
                    {
                        return false;
                    }
                    return UnlockFoundersTokens((byte[])args[1], (int)args[2]);
                case "UpdateAdminAddress":
                    if (!Helpers.RequireArgumentLength(args, 2))
                    {
                        return false;
                    }
                    return UpdateAdminAddress((byte[])args[1]);
            }

            return false;
        }

        /// <summary>
        /// allow allocation of presale purchases by contract administrator. this allows the moonlight team to allocate the 25% of LX tokens sold in the private presale.
        /// as we accepted ETH in addition to NEO&GAS, using a mintTokens method here is not practical.
        /// 1. this method will not allow the presale allocation to exceed the defined amount
        /// 2. this method is permanently disabled once the method `LockPresaleAllocation` has been called.
        /// 3. the state of the `LockPresaleAllocation` can be determined by the public using the method `IsPresaleAllocationLocked` (returns timestamp that lock was put in place)
        /// </summary>
        /// <param name="address"></param>
        /// <param name="amountPurchased"></param>
        /// <returns></returns>
        public static bool AllocatePresalePurchase(byte[] address, BigInteger amountPurchased)
        {
            bool presaleLocked = Storage.Get(Storage.CurrentContext, StorageKeys.PresaleAllocationLocked()).AsBigInteger() > 0;
            if (presaleLocked)
            {
                Runtime.Notify("AllocatePresalePurchase() presaleLocked, can't allocate");
                return false;
            }

            BigInteger presaleAllocationMaxValue = ((ICOTemplate.TokenMaxSupply * (BigInteger)ICOTemplate.PresaleAllocationPercentage()) / 100) * NEP5.factor;
            BigInteger presaleAllocatedValue = Storage.Get(Storage.CurrentContext, StorageKeys.PresaleAllocatedValue()).AsBigInteger();

            if ((presaleAllocatedValue + amountPurchased) > presaleAllocationMaxValue)
            {
                // this purchase will exceed the presale cap.. dont allow
                Runtime.Notify("AllocatePresalePurchase() purchase will exceed presale max allocation");
                return false;
            }

            TokenSale.SetVestingPeriodForAddress(address, amountPurchased);
            Storage.Put(Storage.CurrentContext, StorageKeys.PresaleAllocatedValue(), presaleAllocatedValue + amountPurchased);
            Runtime.Notify("AllocatePresalePurchase() tokens allocated", address, amountPurchased);

            return true;
        }

        /// <summary>
        /// allow contract administrator to migrate the storage of this contract
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static bool ContractMigrate(object[] args)
        {
            // Contract Migrate(byte[] script, byte[] parameter_list, byte return_type, bool need_storage, string name, string version, string author, string email, string description)
            Contract.Migrate((byte[])args[1], (byte[])args[2], (byte)args[3], (ContractPropertyState)args[4], (string)args[5], (string)args[6], (string)args[7], (string)args[8], (string)args[9]);
            return true;
        }

        /// <summary>
        /// initialise the smart contract for use
        /// </summary>
        /// <returns></returns>
        public static bool InitSmartContract()
        {
            if (Helpers.ContractInitialised())
            {
                // contract can only be initialised once
                Runtime.Log("InitSmartContract() contract already initialised");
                return false;
            }

            uint ContractInitTime = Helpers.GetBlockTimestamp();
            Storage.Put(Storage.CurrentContext, StorageKeys.ContractInitTime(), ContractInitTime);

            // assign pre-allocated tokens to the project
            object[] immediateAllocation = ICOTemplate.ImmediateProjectGrowthAllocation();
            object[] vestedAllocation = ICOTemplate.VestedProjectGrowthAllocation();

            BigInteger immediateProjectAllocationValue = ((ICOTemplate.TokenMaxSupply * (BigInteger)immediateAllocation[0]) / 100) * NEP5.factor;
            BigInteger vestedProjectAllocationValue = ((ICOTemplate.TokenMaxSupply * (BigInteger)vestedAllocation[0]) / 100) * NEP5.factor;

            Helpers.SetBalanceOf(ICOTemplate.MoonlightProjectKey(), immediateProjectAllocationValue + vestedProjectAllocationValue);
            Helpers.SetBalanceOfVestedAmount(ICOTemplate.MoonlightProjectKey(), immediateProjectAllocationValue + vestedProjectAllocationValue);

            // lockup a portion of the tokens to be released in the future
            uint vestedGrowthReleaseDate = (uint)vestedAllocation[1] + ContractInitTime;
            object[] vestedTokenPeriod = new object[] { vestedGrowthReleaseDate, vestedProjectAllocationValue };
            StorageMap vestingData = Storage.CurrentContext.CreateMap(StorageKeys.VestedTokenPrefix());
            vestingData.Put(ICOTemplate.MoonlightProjectKey(), vestedTokenPeriod.Serialize());

            // token allocation to MoonlightFounderKeys - update the total supply to include balance - these funds will be unlocked gradually
            BigInteger founderTokenAllocation = ((ICOTemplate.TokenMaxSupply * (BigInteger)ICOTemplate.MoonlightFoundersAllocationPercentage()) / 100) * NEP5.factor;

            // token allocated to presale
            BigInteger presaleAllocationMaxValue = ((ICOTemplate.TokenMaxSupply * (BigInteger)ICOTemplate.PresaleAllocationPercentage()) / 100) * NEP5.factor;

            // update the total supply to reflect the project allocated tokens
            BigInteger totalSupply = immediateProjectAllocationValue + vestedProjectAllocationValue + founderTokenAllocation + presaleAllocationMaxValue;
            Helpers.SetTotalSupply(totalSupply);

            UpdateAdminAddress(ICOTemplate.InitialAdminAccount);
            EnableDEXWhitelisting(ICOTemplate.WhitelistDEXListings());
            Runtime.Log("InitSmartContract() contract initialisation complete");
            return true;
        }

        /// <summary>
        /// once initial presale allocation completed perform lock that prevents allocation being used
        /// </summary>
        /// <returns></returns>
        public static bool LockPresaleAllocation()
        {
            Runtime.Log("LockPresaleAllocation() further presale allocations locked");
            Storage.Put(Storage.CurrentContext, StorageKeys.PresaleAllocationLocked(), Helpers.GetBlockTimestamp());
            return true;
        }

        /// <summary>
        /// the core teams token allocation follow a linear quarterly maturation over 18 months beginning after 6 months
        /// </summary>
        /// <returns></returns>
        public static object[] GetCoreTeamVestingSchedule()
        {
            // calculate the allocation given to each team member
            object[] founderKeys = ICOTemplate.MoonlightFounderKeys();
            BigInteger founderTokenAllocation = ((ICOTemplate.TokenMaxSupply * (BigInteger)ICOTemplate.MoonlightFoundersAllocationPercentage()) / 100) * NEP5.factor;
            BigInteger individualAllocation = founderTokenAllocation / founderKeys.Length;

            uint ContractInitTime = Helpers.GetContractInitTime();
            // determine vesting schedule details for core teams token allocation
            // there will be 7 releases, one each quarter ending 2 years from contract init
            int numberOfTokenReleases = 7;
            BigInteger tokensPerRelease = individualAllocation / numberOfTokenReleases;

            object[] vestingPeriod = new object[14];
            object[] founderReleaseSchedule = ICOTemplate.MoonlightFoundersAllocationReleaseSchedule();
            uint initialReleaseDate = ContractInitTime + (uint)founderReleaseSchedule[0];
            uint releaseFrequency = (uint)founderReleaseSchedule[1];

            BigInteger tokensReleased = tokensPerRelease;
            // this is not the nicest way to populate the vesting schedule array, but it is much cheaper (in terms of processing/gas price) than looping
            vestingPeriod[0] = initialReleaseDate;
            vestingPeriod[1] = tokensPerRelease;
            // 3 months later release another batch of tokens
            tokensReleased += tokensPerRelease;
            vestingPeriod[2] = initialReleaseDate + (releaseFrequency * 1);
            vestingPeriod[3] = tokensPerRelease;
            // 3 months later release another batch of tokens
            tokensReleased += tokensPerRelease;
            vestingPeriod[4] = initialReleaseDate + (releaseFrequency * 2);
            vestingPeriod[5] = tokensPerRelease;
            // 3 months later release another batch of tokens
            tokensReleased += tokensPerRelease;
            vestingPeriod[6] = initialReleaseDate + (releaseFrequency * 3);
            vestingPeriod[7] = tokensPerRelease;
            // 3 months later release another batch of tokens
            tokensReleased += tokensPerRelease;
            vestingPeriod[8] = initialReleaseDate + (releaseFrequency * 4);
            vestingPeriod[9] = tokensPerRelease;
            // 3 months later release another batch of tokens
            tokensReleased += tokensPerRelease;
            vestingPeriod[10] = initialReleaseDate + (releaseFrequency * 5);
            vestingPeriod[11] = tokensPerRelease;
            // 3 months later release the last of the tokens
            vestingPeriod[12] = initialReleaseDate + (releaseFrequency * 6);
            vestingPeriod[13] = individualAllocation - tokensReleased;

            /*
             * Runtime.Notify("VestingSchedule", Helpers.SerializeArray(vestingPeriod));
             * a serialised copy of this array ends up with values such as (dates subject to change):
                0e
                04 292cf05b          5bf02c29            1542466601         Saturday, November 17, 2018 2:56:41 PM
                07 6ddb810adb0301    0103db0a81db6d      285714285714285
                04 0979685c	         5c687909            1550350601         Saturday, February 16, 2019 8:56:41 PM
                07 dab60315b60702    0207b61503b6da      571428571428570
                04 e9c5e05c          5ce0c5e9            1558234601         Sunday, May 19, 2019 2:56:41 AM
                07 4792851f910b03    030b911f859247      857142857142855 
                04 c912595d          5d5912c9            1566118601         Sunday, August 18, 2019 8:56:41 AM
                07 b46d072a6c0f04    040f6c2a076db4      1142857142857140
                04 a95fd15d          5dd15fa9            1574002601         Sunday, November 17, 2019 2:56:41 PM
                07 21498934471305    05134734894921      1428571428571425
                04 89ac495e          5e49ac89            1581886601         Sunday, February 16, 2020 8:56:41 PM
                07 8e240b3f221706    0617223f0b248e      1714285714285710
                04 69f9c15e          5ec1f969            1589770601         Monday, May 18, 2020 2:56:41 AM
                07 00008d49fd1a07    071afd498d0000      2000000000000000              
             */
            return vestingPeriod;
        }

        /// <summary>
        /// allow an administrator to request the unlocking of founder tokens
        /// </summary>
        /// <param name="address">founders script hash</param>
        /// <param name="roundNumber">1-7</param>
        /// <returns></returns>
        public static bool UnlockFoundersTokens(byte[] address, int roundNumber)
        {

            if (address.Length != 20)
            {
                Runtime.Log("UnlockFoundersTokens() invalid address supplied");
                return false;
            }

            byte[] roundKey = address.Concat(((BigInteger)roundNumber).AsByteArray());
            StorageMap unlockedRounds = Storage.CurrentContext.CreateMap(StorageKeys.FounderTokenUnlockRound());

            bool roundPreviouslyUnlocked = unlockedRounds.Get(roundKey).AsBigInteger() > 0;

            if (roundPreviouslyUnlocked)
            {
                Runtime.Log("UnlockFoundersTokens() round already unlocked");
                return false;
            }

            object[] foundersVestingPeriod = GetCoreTeamVestingSchedule();

            uint currentTimestamp = Helpers.GetBlockTimestamp();
            int roundIndex = (roundNumber * 2) - 2;
            int roundValueIndex = roundIndex + 1;

            if (roundIndex < 0)
            {
                Runtime.Log("UnlockFoundersTokens() invalid round index (<0)");
                return false;
            }

            uint roundReleaseDate = (uint)foundersVestingPeriod[roundIndex];
            BigInteger roundReleaseAmount = (BigInteger)foundersVestingPeriod[roundValueIndex];

            if (currentTimestamp < roundReleaseDate)
            {
                Runtime.Log("UnlockFoundersTokens() not scheduled for release");
                return false;
            }


            object[] founderKeys = ICOTemplate.MoonlightFounderKeys();
            for (int i = 0; i < founderKeys.Length; i++)
            {
                byte[] founderKey = (byte[])founderKeys[i];
                if (founderKey == address)
                {
                    Runtime.Notify("UnlockFoundersTokens() releasing funds. currentTimestamp / roundReleaseDate / roundReleaseAmount", currentTimestamp, roundReleaseDate, roundReleaseAmount);
                    Helpers.SetBalanceOf(founderKey, NEP5.BalanceOf(founderKey) + roundReleaseAmount);            // set new balance for destination account
                    unlockedRounds.Put(roundKey, "1");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// allow the contract administrator to update the admin address
        /// </summary>
        /// <param name="newAdminAddress"></param>
        /// <returns></returns>
        public static bool UpdateAdminAddress(byte[] newAdminAddress)
        {
            Storage.Put(Storage.CurrentContext, StorageKeys.ContractAdmin(), newAdminAddress);
            return true;
        }

        /// <summary>
        /// allow admin to toggle dex whitelist on or off
        /// </summary>
        /// <param name="isEnabled"></param>
        /// <returns></returns>
        public static bool EnableDEXWhitelisting(bool isEnabled)
        {
            Storage.Put(Storage.CurrentContext, StorageKeys.WhiteListDEXSettingChecked(), isEnabled ? "1" : "0");
            return true;
        }

        /// <summary>
        /// add a DEX contract address to the whitelist
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static bool WhitelistDEXAdd(byte[] address)
        {
            if (address.Length != 20)
            {
                return false;
            }

            StorageMap dexList = Storage.CurrentContext.CreateMap(StorageKeys.WhiteListedDEXList());
            dexList.Put(address, "1");
            Runtime.Notify("WhitelistDEXAdd() added contract to whitelist", address);

            return true;
        }

        /// <summary>
        /// remove a DEX contract address from the whitelist
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static bool WhitelistDEXRemove(byte[] address)
        {
            if (address.Length != 20)
            {
                return false;
            }

            StorageMap dexList = Storage.CurrentContext.CreateMap(StorageKeys.WhiteListedDEXList());
            dexList.Delete(address);
            Runtime.Notify("WhitelistDEXRemove() removed contract from whitelist", address);

            return true;
        }

    }
}
