using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System.Numerics;

namespace Neo.SmartContract
{
    public class KYC : Framework.SmartContract
    {
        public static string[] GetKYCMethods() => new string[] {
            "AddAddress",
            "crowdsale_status",
            "GetBlockHeight",
            "GetGroupMaxContribution",
            "GetGroupNumber",
            "GetGroupUnlockBlock",
            "GroupParticipationIsUnlocked",
            "RevokeAddress",
            "SetGroupMaxContribution",
            "SetGroupUnlockBlock"
        };

        public static object HandleKYCOperation(string operation, params object[] args)
        {
            // neo-compiler doesn't support switch blocks with too many case statements due to c# compiler optimisations
            // * IL_0004 Call System.UInt32 <PrivateImplementationDetails>::ComputeStringHash(System.String) ---> System.Exception: not supported on neovm now.
            // therefore, extra if statements required for more than 6 operations
            if (operation == "crowdsale_status")
            {
                // test if an address is whitelisted
                if (!Helpers.RequireArgumentLength(args, 1))
                {
                    return false;
                }
                return AddressIsWhitelisted((byte[])args[0]);
            }
            else if (operation == "GetGroupNumber")
            {
                // allow people to check which group they have been assigned to during the whitelist process
                if (!Helpers.RequireArgumentLength(args, 1))
                {
                    return false;
                }
                return GetWhitelistGroupNumber((byte[])args[0]);
            }
            else if (operation == "GroupParticipationIsUnlocked")
            {
                // allow people to check if their group is unlocked (bool)
                if (!Helpers.RequireArgumentLength(args, 1))
                {
                    return false;
                }
                return GroupParticipationIsUnlocked((int)args[0]);
            } else if (operation == "GetBlockHeight")
            {
                // expose a method to retrieve current block height
                return Blockchain.GetHeight();
            }

            switch (operation)
            {
                case "AddAddress":
                    // add an address to the kyc whitelist
                    if (!Helpers.RequireArgumentLength(args, 2))
                    {
                        return false;
                    }
                    return AddAddress((byte[])args[0], (int)args[1]);
                case "GetGroupMaxContribution":
                    // get the maximum amount of LX that can be purchased for group
                    if (!Helpers.RequireArgumentLength(args, 1))
                    {
                        return false;
                    }
                    return GetGroupMaxContribution((BigInteger)args[0]);
                case "GetGroupUnlockBlock":
                    // allow people to check the block height their group will be unlocked (uint)
                    if (!Helpers.RequireArgumentLength(args, 1))
                    {
                        return false;
                    }
                    return GetGroupUnlockBlock((BigInteger)args[0]);
                case "RevokeAddress":
                    // remove an address to the kyc whitelist
                    if (!Helpers.RequireArgumentLength(args, 1))
                    {
                        return false;
                    }
                    return RevokeAddress((byte[])args[0]);
                case "SetGroupMaxContribution":
                    if (!Helpers.RequireArgumentLength(args, 2))
                    {
                        return false;
                    }
                    return SetGroupMaxContribution((BigInteger)args[0], (uint)args[1]);
                case "SetGroupUnlockBlock":
                    if (!Helpers.RequireArgumentLength(args, 2))
                    {
                        return false;
                    }
                    return SetGroupUnlockBlock((BigInteger)args[0], (uint)args[1]);

            }

            return false;
        }

        /// <summary>
        /// add an address to the kyc whitelist
        /// </summary>
        /// <param name="address"></param>
        public static bool AddAddress(byte[] address, int groupNumber)
        {
            if (address.Length != 20 || groupNumber <= 0)
            {
                return false;
            }

            if (Helpers.VerifyIsAdminAccount())
            {
                StorageMap kycWhitelist = Storage.CurrentContext.CreateMap(StorageKeys.KYCWhitelistPrefix());
                kycWhitelist.Put(address, groupNumber);
                return true;
            }
            return false;
        }

        /// <summary>
        /// determine if the given address is whitelisted by testing if group number > 0
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static bool AddressIsWhitelisted(byte[] address)
        {
            if (address.Length != 20)
            {
                return false;
            }

            BigInteger whitelisted = GetWhitelistGroupNumber(address);
            return whitelisted > 0;
        }

        /// <summary>
        /// get the maximum number of LX that can be purchased by groupNumber during the public sale
        /// </summary>
        /// <param name="groupNumber"></param>
        /// <returns></returns>
        public static BigInteger GetGroupMaxContribution(BigInteger groupNumber)
        {
            StorageMap contributionLimits = Storage.CurrentContext.CreateMap(StorageKeys.GroupContributionAmountPrefix());
            BigInteger maxContribution = contributionLimits.Get(groupNumber.AsByteArray()).AsBigInteger();

            if (maxContribution > 0)
            {
                return maxContribution;
            }

            return ICOTemplate.MaximumContributionAmount();
        }

        /// <summary>
        /// helper method to retrieve the stored group unlock block height
        /// </summary>
        /// <param name="groupNumber"></param>
        /// <returns></returns>
        public static uint GetGroupUnlockBlock(BigInteger groupNumber)
        {
            if (groupNumber <= 0)
            {
                return 0;
            }

            StorageMap unlockBlock = Storage.CurrentContext.CreateMap(StorageKeys.GroupUnlockPrefix());
            return (uint)unlockBlock.Get(groupNumber.AsByteArray()).AsBigInteger();
        }

        /// <summary>
        /// retrieve the group number the whitelisted address is in
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static BigInteger GetWhitelistGroupNumber(byte[] address)
        {
            if (address.Length != 20)
            {
                return 0;
            }

            StorageMap kycWhitelist = Storage.CurrentContext.CreateMap(StorageKeys.KYCWhitelistPrefix());
            return kycWhitelist.Get(address).AsBigInteger();
        }

        /// <summary>
        /// determine if groupNumber is eligible to participate in public sale yet
        /// </summary>
        /// <param name="groupNumber"></param>
        /// <returns></returns>
        public static bool GroupParticipationIsUnlocked(int groupNumber)
        {
            if (groupNumber <= 0)
            {
                return false;
            }

            uint unlockBlockNumber = GetGroupUnlockBlock(groupNumber);
            return unlockBlockNumber > 0 && unlockBlockNumber <= Blockchain.GetHeight();
        }

        /// <summary>
        /// remove an address from the whitelist
        /// </summary>
        /// <param name="address"></param>
        public static bool RevokeAddress(byte[] address)
        {
            if (address.Length != 20)
            {
                return false;
            }

            if (Helpers.VerifyIsAdminAccount())
            {
                StorageMap kycWhitelist = Storage.CurrentContext.CreateMap(StorageKeys.KYCWhitelistPrefix());
                kycWhitelist.Delete(address);
                return true;
            }
            return false;
        }

        /// <summary>
        /// allow administrator to set the maximum contribution amount allowed for a presale group
        /// </summary>
        /// <param name="groupNumber"></param>
        /// <param name="maxContribution">max number of LX that can be purchased by this group</param>
        /// <returns></returns>
        public static bool SetGroupMaxContribution(BigInteger groupNumber, uint maxContribution)
        {
            if (groupNumber <= 0 || maxContribution <= 0)
            {
                return false;
            }

            if (Helpers.VerifyIsAdminAccount())
            {
                StorageMap contributionLimits = Storage.CurrentContext.CreateMap(StorageKeys.GroupContributionAmountPrefix());
                contributionLimits.Put(groupNumber.AsByteArray(), maxContribution);
                return true;
            }

            return false;
        }

        /// <summary>
        /// set the block number that a specific group is allowed to participate in the ICO
        /// </summary>
        /// <param name="groupNumber"></param>
        /// <param name="unlockBlockNumber">group will be able to participate at this block height</param>
        /// <returns></returns>
        public static bool SetGroupUnlockBlock(BigInteger groupNumber, uint unlockBlockNumber)
        {
            if (groupNumber <= 0 || unlockBlockNumber <= 0)
            {
                return false;
            }

            if (Helpers.VerifyIsAdminAccount())
            {
                Runtime.Notify("SetGroupUnlockBlock() groupNumber / unlockBlockNumber", groupNumber, unlockBlockNumber);
                StorageMap unlockBlocks = Storage.CurrentContext.CreateMap(StorageKeys.GroupUnlockPrefix());
                unlockBlocks.Put(groupNumber.AsByteArray(), unlockBlockNumber);
                return true;
            }

            return false;
        }
    }
}
