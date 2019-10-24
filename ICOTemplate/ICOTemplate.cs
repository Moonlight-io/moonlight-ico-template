using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System.Numerics;

namespace Neo.SmartContract
{
    /// <summary>
    /// Updated ico template for the neo ecosystem
    /// </summary>
    public class ICOTemplate : Framework.SmartContract
    {
        ///<remarks>
        /// START user configurable fields
        ///</remarks>

        /// <summary>
        /// this is the initial admin account responsible for initialising the contract (reversed byte array of contract address)
        /// </summary>
        public static readonly byte[] InitialAdminAccount = { 100, 160, 44, 55, 242, 105, 87, 175, 125, 97, 75, 228, 143, 76, 167, 75, 245, 65, 162, 123 };

        /// <summary>
        /// project token allocation will be assigned here and subject to vesting criteria defined in the whitepaper (see below for details)
        /// </summary>
        public static byte[] MoonlightProjectKey() => new byte[] { 124, 126, 46, 158, 164, 73, 102, 110, 177, 73, 54, 125, 123, 109, 137, 234, 83, 108, 79, 40 };

        /// <summary>
        /// founder tokens will be assigned to the following addresses and are subject to different token vesting rules (see below for details)
        /// </summary>
        public static object[] MoonlightFounderKeys() => new object[]
        {
            new byte[] { 55, 250, 125, 221, 52, 232, 7, 221, 193, 108, 232, 36, 200, 209, 139, 35, 6, 111, 42, 242 },       // founder1
            new byte[] { 205, 158, 64, 30, 243, 102, 24, 159, 111, 117, 200, 21, 229, 221, 26, 159, 68, 122, 35, 57 }      // founder2
        };

        /// <summary>
        /// NEP5.1 definition: the name we will give our token
        /// </summary>
        public static string TokenName() => "Your Token Name";

        /// <summary>
        /// NEP5.1 definition: the trading symbol we will give our NEP5.1 token
        /// </summary>
        public static string TokenSymbol() => "TokenSymbol";

        /// <summary>
        /// NEP5.1 definition: the number of tokens that can be minted
        /// </summary>
        public const ulong TokenMaxSupply = 1000000000;

        /// <summary>
        /// this is the default maximum amount of LX that can be purchased during the public sale
        /// this value can be modified on a group by group basis by the contract administrator (KYC::SetGroupMaxContribution())
        /// </summary>
        /// <returns></returns>
        public static ulong MaximumContributionAmount() => 200000;

        /// <summary>
        /// does your ICO accept NEO for payments?
        /// </summary>
        public static bool ICOAllowsNEO() => true;

        /// <summary>
        /// how many tokens will you get for a unit of neo
        /// </summary>
        /// <returns></returns>
        public static ulong ICONeoToTokenExchangeRate() => 2000;

        /// <summary>
        /// does your ICO accept GAS for payments?
        /// </summary>
        public static bool ICOAllowsGAS() => true;

        /// <summary>
        /// how many tokens will you get for a unit of gas
        /// </summary>
        /// <returns></returns>
        public static ulong ICOGasToTokenExchangeRate() => 800;

        /// <summary>
        /// will the ICO be using the token vesting scheme
        /// </summary>
        public static bool UseTokenVestingPeriod() => true;

        /// <summary>
        /// tokens purchased over the amount of 250,000 during the presale will be subject to a vesting period of 3 months
        /// </summary>
        public static object[] VestingBracketOne() => new object[] { 250000, 7257600 };

        /// <summary>
        /// tokens purchased over the amount of 5,000,000 during the presale will be subject to a vesting period of 6 months
        /// </summary>
        public static object[] VestingBracketTwo() => new object[] { 5000000, 14515200 };

        /// <summary>
        /// 25% of tokens allocated to private presale
        /// </summary>
        /// <returns></returns>
        public static int PresaleAllocationPercentage() => 25;

        /// <summary>
        /// 20% of tokens allocated for immediate project growth will not be subject to any vesting time
        /// </summary>
        public static object[] ImmediateProjectGrowthAllocation() => new object[] { 20, 0 };

        /// <summary>
        /// percentage of tokens allocated to moonlight founders subject to a special vestment period, as defined in the whitepaper
        /// </summary>
        public static int MoonlightFoundersAllocationPercentage() => 10;

        /// <summary>
        /// release the first batch of founder tokens after 6months (15768000 seconds) and then subsequent releases every 3months (7884000)
        /// </summary>
        /// <returns></returns>
        public static object[] MoonlightFoundersAllocationReleaseSchedule() => new object[] { 15768000, 7884000 };

        /// <summary>
        /// 20% of tokens allocated for future project growth will be locked for two years
        /// </summary>
        public static object[] VestedProjectGrowthAllocation() => new object[] { 20, 63072000 };

        /// <summary>
        /// list NEPs supported by this contract
        /// </summary>
        /// <returns></returns>
        public static string SupportedStandards() => "{\"NEP-5\", \"NEP-10\"}";

        /// <summary>
        /// should whitelisting of DEX transfer/transferFrom methods be checked
        /// </summary>
        /// <returns></returns>
        public static bool WhitelistDEXListings() => true;

        ///<remarks>
        /// END user configurable fields
        ///</remarks>

        /// <summary>
        /// the entry point for smart contract execution
        /// </summary>
        /// <param name="operation">string to determine execution operation performed</param>
        /// <param name="args">optional arguments, context specific depending on operation</param>
        /// <returns></returns>
        public static object Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Application)
            {
                // test if a nep5 method is being invoked
                foreach (string nepMethod in NEP5.GetNEP5Methods())
                {
                    if (nepMethod == operation)
                    {
                        return NEP5.HandleNEP5Operation(operation, args, ExecutionEngine.CallingScriptHash, ExecutionEngine.EntryScriptHash);
                    }
                }

                // test if a kyc method is being invoked
                foreach (string kycMethod in KYC.GetKYCMethods())
                {
                    if (kycMethod == operation)
                    {
                        return KYC.HandleKYCOperation(operation, args);
                    }
                }

                // test if a helper/misc method is being invoked
                foreach (string helperMethod in Helpers.GetHelperMethods())
                {
                    if (helperMethod == operation)
                    {
                        return Helpers.HandleHelperOperation(operation, args);
                    }
                }

                if (operation == "admin" && Helpers.VerifyIsAdminAccount())
                {
                    // allow access to administration methods
                    string adminOperation = (string)args[0];
                    foreach (string adminMethod in Administration.GetAdministrationMethods())
                    {
                        if (adminMethod == adminOperation)
                        {
                            return Administration.HandleAdministrationOperation(adminOperation, args);
                        }
                    }
                    return false;
                }
            }
            else if (Runtime.Trigger == TriggerType.Verification)
            {
                if (Helpers.VerifyIsAdminAccount())
                {
                    return true;
                }

                // test if this transaction is allowed
                object[] transactionData = Helpers.GetTransactionAndSaleData();
                return TokenSale.CanUserParticipateInSale(transactionData);
            }

            return false;
        }

    }
}
