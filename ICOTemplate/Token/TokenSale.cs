using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.ComponentModel;
using System.Numerics;


namespace Neo.SmartContract
{
    public class TokenSale : Framework.SmartContract
    {
        [DisplayName("refund")]
        public static event Action<byte[], BigInteger, BigInteger> refund;

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> transfer;

        /// <summary>
        /// determine if user can participate in the token sale yet
        /// </summary>
        /// <param name="sender"></param>
        /// <returns></returns>
        public static bool CanUserParticipateInSale(object[] transactionData)
        {
            Transaction tx = (Transaction)transactionData[0];
            byte[] sender = (byte[])transactionData[1];
            byte[] receiver = (byte[])transactionData[2];
            ulong receivedNEO = (ulong)transactionData[3];
            ulong receivedGAS = (ulong)transactionData[4];
            BigInteger whiteListGroupNumber = (BigInteger)transactionData[5];
            BigInteger crowdsaleAvailableAmount = (BigInteger)transactionData[6];
            BigInteger groupMaximumContribution = (BigInteger)transactionData[7];
            BigInteger totalTokensPurchased = (BigInteger)transactionData[8];
            BigInteger neoRemainingAfterPurchase = (BigInteger)transactionData[9];
            BigInteger gasRemainingAfterPurchase = (BigInteger)transactionData[10];
            BigInteger totalContributionBalance = (BigInteger)transactionData[11];

            if (whiteListGroupNumber <= 0)
            {
                Runtime.Notify("CanUserParticipate() sender is not whitelisted", sender);
                return false;
            }

            if (!KYC.GroupParticipationIsUnlocked((int)whiteListGroupNumber))
            {
                Runtime.Notify("CanUserParticipate() sender cannot participate yet", sender);
                return false;
            }

            if (crowdsaleAvailableAmount <= 0)
            {
                // total supply has been exhausted
                Runtime.Notify("CanUserParticipate() crowdsaleAvailableAmount is <= 0", crowdsaleAvailableAmount);
                return false;
            }

            if (totalContributionBalance > groupMaximumContribution)
            {
                // don't allow this purchase exceed the group cap
                Runtime.Notify("CanUserParticipate() senders purchase will exceed maxContribution cap", sender, totalContributionBalance, groupMaximumContribution);
                refund(sender, receivedNEO, receivedGAS);
                return false;
            }

            return true;
        }

        /// <summary>
        /// mint tokens is called when a user wishes to purchase tokens
        /// </summary>
        /// <returns></returns>
        public static bool MintTokens()
        {
            object[] transactionData = Helpers.GetTransactionAndSaleData();
            Transaction tx = (Transaction)transactionData[0];
            byte[] sender = (byte[])transactionData[1];
            byte[] receiver = (byte[])transactionData[2];
            ulong receivedNEO = (ulong)transactionData[3];
            ulong receivedGAS = (ulong)transactionData[4];
            BigInteger whiteListGroupNumber = (BigInteger)transactionData[5];
            BigInteger crowdsaleAvailableAmount = (BigInteger)transactionData[6];
            BigInteger groupMaximumContribution = (BigInteger)transactionData[7];
            BigInteger totalTokensPurchased = (BigInteger)transactionData[8];
            BigInteger neoRemainingAfterPurchase = (BigInteger)transactionData[9];
            BigInteger gasRemainingAfterPurchase = (BigInteger)transactionData[10];
            BigInteger totalContributionBalance = (BigInteger)transactionData[11];

            if (!CanUserParticipateInSale(transactionData))
            {
                Runtime.Notify("MintTokens() CanUserParticipate failed", false);
                return false;
            }

            byte[] lastTransactionHash = Storage.Get(Storage.CurrentContext, StorageKeys.MintTokensLastTX());
            if (lastTransactionHash == tx.Hash)
            {
                // ensure that minTokens doesnt process the same transaction more than once
                Runtime.Notify("MintTokens() not processing duplicate tx.Hash", tx.Hash);
                return false;
            }

            Storage.Put(Storage.CurrentContext, StorageKeys.MintTokensLastTX(), tx.Hash);
            Runtime.Notify("MintTokens() receivedNEO / receivedGAS", receivedNEO, receivedGAS);

            if (neoRemainingAfterPurchase > 0 || gasRemainingAfterPurchase > 0)
            {
                // this purchase would have exceed the allowed max supply so we spent what we could and will refund the remainder
                refund(sender, neoRemainingAfterPurchase, gasRemainingAfterPurchase);
            }

            BigInteger senderAmountSubjectToVesting = SubjectToVestingPeriod(sender);
            BigInteger newTokenBalance = NEP5.BalanceOf(sender) + totalTokensPurchased + senderAmountSubjectToVesting;

            Helpers.SetBalanceOf(sender, newTokenBalance);
            Helpers.SetBalanceOfSaleContribution(sender, totalContributionBalance);
            Helpers.SetTotalSupply(totalTokensPurchased);

            transfer(null, sender, totalTokensPurchased);
            return true;
        }

        /// <summary>
        /// set a vesting schedule, as defined in the whitepaper, for tokens purchased during the presale
        /// </summary>
        /// <param name="address"></param>
        /// <param name="tokenBalance"></param>
        /// <returns></returns>
        public static bool SetVestingPeriodForAddress(byte[] address, BigInteger tokensPurchased)
        {
            if (!ICOTemplate.UseTokenVestingPeriod())
            {
                return false;
            }

            if (address.Length != 20)
            {
                return false;
            }

            object[] vestingOne = ICOTemplate.VestingBracketOne();
            object[] vestingTwo = ICOTemplate.VestingBracketTwo();
            BigInteger bracketOneThreshold = (BigInteger)vestingOne[0] * NEP5.factor;
            BigInteger bracketTwoThreshold = (BigInteger)vestingTwo[0] * NEP5.factor;
            BigInteger currentAvailableBalance = 0;        // how many tokens will be immediately available to the owner

            uint currentTimestamp = Helpers.GetContractInitTime();
            uint bracketOneReleaseDate = (uint)vestingOne[1] + currentTimestamp;
            uint bracketTwoReleaseDate = (uint)vestingTwo[1] + currentTimestamp;
            StorageMap vestingData = Storage.CurrentContext.CreateMap(StorageKeys.VestedTokenPrefix());

            if (tokensPurchased > bracketTwoThreshold)
            {
                // user has purchased enough tokens to fall under the second vesting period restriction
                // calculate the difference between the bracketOne and bracketTwo thresholds to calculate how much should be released after bracketOne lapses
                BigInteger bracketOneReleaseAmount = bracketTwoThreshold - bracketOneThreshold;
                // the remainder will be released after the bracket two release date
                BigInteger bracketTwoReleaseAmount = tokensPurchased - bracketOneReleaseAmount - bracketOneThreshold;
                object[] lockoutTimes = new object[] { bracketOneReleaseDate, bracketOneReleaseAmount, bracketTwoReleaseDate, bracketTwoReleaseAmount };
                vestingData.Put(address, lockoutTimes.Serialize());
            }
            else
            {
                // user has purchased enough tokens to fall under the first vesting period restriction
                // calculate the difference between amount purchased and bracketOne threshold to calculate how much should be released after the bracketOne lapses
                BigInteger bracketOneReleaseAmount = tokensPurchased - bracketOneThreshold;
                object[] lockoutTimes = new object[] { bracketOneReleaseDate, bracketOneReleaseAmount };
                vestingData.Put(address, lockoutTimes.Serialize());
            }

            // ensure the total amount purchased is saved
            Helpers.SetBalanceOf(address, tokensPurchased);
            Helpers.SetBalanceOfVestedAmount(address, tokensPurchased);
            return true;
        }

        /// <summary>
        /// return the amount of tokens that are subject to vesting period
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static BigInteger SubjectToVestingPeriod(byte[] address)
        {
            BigInteger amountSubjectToVesting = 0;

            if (address.Length != 20)
            {
                return amountSubjectToVesting;
            }

            object[] tokensVesting = PublicTokensLocked(address);
            uint currentTimestamp = Helpers.GetBlockTimestamp();

            if (tokensVesting.Length > 0)
            {
                // this account has some kind of vesting period
                for (int i = 0; i < tokensVesting.Length; i++)
                {
                    int j = i + 1;
                    uint releaseDate = (uint)tokensVesting[i];
                    BigInteger releaseAmount = (BigInteger)tokensVesting[j];

                    if(currentTimestamp < releaseDate)
                    {
                        // the release date has not yet occurred. add the releaseAmount to the balance
                        amountSubjectToVesting += releaseAmount;
                    }
                    i++;
                }
            }

            return amountSubjectToVesting;
        }

        /// <summary>
        /// will return an array of token release dates if the user purchased in excess of the defined amounts
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static object[] PublicTokensLocked(byte[] address)
        {
            StorageMap vestingData = Storage.CurrentContext.CreateMap(StorageKeys.VestedTokenPrefix());
            byte[] storedData = vestingData.Get(address);

            if (storedData.Length > 0)
            {
                return (object[])storedData.Deserialize();
            }
            return new object[] { };
        }
    }
}
