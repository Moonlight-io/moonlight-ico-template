using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Neo.SmartContract
{
    public class NEP5 : Framework.SmartContract
    {
        // define the global system assets NEO/GAS
        public static readonly byte[] NEO = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };
        public static readonly byte[] GAS = { 231, 45, 40, 105, 121, 238, 108, 177, 183, 230, 93, 253, 223, 178, 227, 132, 16, 11, 141, 20, 142, 119, 88, 222, 66, 228, 22, 139, 113, 121, 44, 96 };

        /// <summary>
        /// NEP5.1 definition: number of decimals for this token - probably best to leave this as-is
        /// </summary>
        public static byte TokenDecimals() => 8;

        /// <summary>
        /// factor used to get the "real" amount proportional to TokenDecimals
        /// </summary>
        public const ulong factor = 100000000;

        /// <summary>
        /// there will be 1billion tokens minted 
        /// </summary>
        public const ulong TokenMaxSupplyDecimals = ICOTemplate.TokenMaxSupply * factor;

        /// <summary>
        /// a list of the NEP5 operations the contract supports
        /// </summary>
        /// <returns></returns>
        public static string[] GetNEP5Methods() => new string[] {
            "name", "symbol", "decimals", "totalSupply", "balanceOf",
            "transfer", "transferFrom", "approve", "allowance",
            "crowdsale_available_amount", "mintTokens"
        };

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> transfer;

        public static object HandleNEP5Operation(string operation, object[] args, byte[] caller, byte[] entry)
        {
            //{ "name", "symbol", "decimals", "totalSupply", "balanceOf", "transfer", "transferFrom", "approve", "allowance" };
            if (operation == "name")
            {
                // the name of the token
                return ICOTemplate.TokenName();
            }

            if (operation == "symbol")
            {
                // the symbol of the token
                return ICOTemplate.TokenSymbol();
            }

            if (operation == "decimals")
            {
                // decimals to determine fractions of tokens
                return TokenDecimals();
            }

            if (operation == "totalSupply")
            {
                // the total number of tokens minted
                return TotalSupply();
            }

            if (operation == "balanceOf")
            {
                // retreive the balance of an address
                if (!Helpers.RequireArgumentLength(args, 1))
                {
                    // BalanceOf() requires at least 1 argument - the address to check the balance of
                    return false;
                }

                return BalanceOf((byte[])args[0]);
            }

            if (operation == "transfer")
            {
                // transfer tokens from one address to another
                if (!Helpers.RequireArgumentLength(args, 3))
                {
                    // Transfer() requires 3 arguments: from, to, amount
                    return false;
                }

                return Transfer((byte[])args[0], (byte[])args[1], (BigInteger)args[2], caller, entry);
            }


            if (operation == "transferFrom")
            {
                // transfer tokens from one address to another
                if (!Helpers.RequireArgumentLength(args, 4))
                {
                    // TransferFrom() requires 4 arguments: spender, from, to, amount
                    return false;
                }

                return TransferFrom((byte[])args[0], (byte[])args[1], (byte[])args[2], (BigInteger)args[3], caller, entry);
            }

            if (operation == "approve")
            {
                // approve a third party to transfer tokens from one address to another
                if (!Helpers.RequireArgumentLength(args, 3))
                {
                    // Approve() requires 3 arguments: from, spender, amount
                    return false;
                }

                return Approve((byte[])args[0], (byte[])args[1], (BigInteger)args[2], caller, entry);
            }

            if (operation == "allowance")
            {
                // retreive the authorised balance of an address
                if (!Helpers.RequireArgumentLength(args, 2))
                {
                    // Allowance() requires 2 arguments: from, to
                    return false;
                }

                return Allowance((byte[])args[0], (byte[])args[1]);
            }

            // check how many tokens left for purchase
            if (operation == "crowdsale_available_amount")
            {
                return CrowdsaleAvailableAmount();
            }

            if (operation == "mintTokens")
            {
                return TokenSale.MintTokens();
            }
            return false;
        }

        /// <summary>
        ///  return the amount of tokens that the "to" account can transfer from the "from" acount.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="spender"></param>
        /// <returns></returns>
        public static BigInteger Allowance(byte[] from, byte[] spender)
        {
            if (from.Length != 20 || spender.Length != 20)
            {
                Runtime.Log("Allowance() invalid from|to address supplied");
                return 0;
            }

            StorageMap allowances = Storage.CurrentContext.CreateMap(StorageKeys.TransferAllowancePrefix());
            return allowances.Get(from.Concat(spender)).AsBigInteger();
        }

        /// <summary>
        /// return the amount of tokens left for purchase
        /// </summary>
        /// <returns></returns>
        public static BigInteger CrowdsaleAvailableAmount()
        {
            BigInteger amountSold = (ICOTemplate.TokenMaxSupply * factor) - NEP5.TotalSupply();
            return amountSold;
        }

        /// <summary>
        /// approve the "spender" account to transfer "approvedValue" tokens from the "from" acount.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="spender"></param>
        /// <param name="amount"></param>
        /// <param name="caller"></param>
        /// <param name="entry"></param>
        /// <returns></returns>
        public static bool Approve(byte[] from, byte[] spender, BigInteger amount, byte[] caller, byte[] entry)
        {
            if (caller != entry && !Helpers.IsContractWhitelistedDEX(caller))
            {
                from = caller;
            }

            if (amount < 0 || from == spender)
            {
                // don't accept a meaningless value
                Runtime.Log("Approve() invalid transfer amount or from==to");
                return false;
            }

            if (from.Length != 20 || spender.Length != 20)
            {
                Runtime.Log("Approve() (from|spender).Length != 20");
                return false;
            }

            if (!Runtime.CheckWitness(from))
            {
                // ensure transaction is signed properly by the owner of the tokens
                Runtime.Log("Approve() CheckWitness failed");
                return false;
            }

            BigInteger fromBalance = BalanceOf(from);                   // retrieve balance of originating account
            if (fromBalance < amount)
            {
                Runtime.Log("Approve() fromBalance < approveValue");
                // don't approve if funds not available
                return false;
            }

            Helpers.SetAllowanceAmount(from.Concat(spender), amount);
            return true;
        }

        /// <summary>
        /// retreive the balance of an address
        /// </summary>
        /// <param name="account">address to check balance of</param>
        /// <returns>number of tokens</returns>
        public static BigInteger BalanceOf(byte[] account)
        {
            if (account.Length != 20)
            {
                Runtime.Log("BalanceOf() invalid address supplied");
                return 0;
            }

            StorageMap balances = Storage.CurrentContext.CreateMap(StorageKeys.BalancePrefix());

            BigInteger amountSubjectToVesting = TokenSale.SubjectToVestingPeriod(account);
            BigInteger userBalance = balances.Get(account).AsBigInteger() - amountSubjectToVesting;
            if (userBalance < 0)
            {
                userBalance = 0;
            }

            return userBalance.AsByteArray().Concat(new byte[] { }).AsBigInteger();
        }

        /// <summary>
        /// NEP5: get the number of tokens that have been minted
        /// </summary>
        /// <returns>number of tokens minted</returns>
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, StorageKeys.TokenTotalSupply()).AsBigInteger();
        }

        /// <summary>
        /// NEP5: Transfer tokens from one account to another
        /// </summary>
        /// <param name="from">sender address</param>
        /// <param name="to">recipient address</param>
        /// <param name="amount">number of tokens to transfer</param>
        /// <param name="caller"></param>
        /// <param name="entry"></param>
        /// <returns></returns>
        public static bool Transfer(byte[] from, byte[] to, BigInteger amount, byte[] caller, byte[] entry)
        {
            if (caller != entry && !Helpers.IsContractWhitelistedDEX(caller))
            {
                from = caller;
            }

            if (from.Length != 20 || to.Length != 20)
            {
                Runtime.Log("Transfer() (from|to).Length != 20");
                return false;
            }

            if (amount < 0)
            {
                Runtime.Log("Transfer() invalid transfer amount must be >= 0");
                throw new Exception();
            }

            BigInteger fromBalance = BalanceOf(from);                   // retrieve balance of originating account
            if (fromBalance < amount)
            {
                Runtime.Log("Transfer() fromBalance < transferValue");
                // don't transfer if funds not available
                return false;
            }

            if (amount == 0 || from == to)
            {
                // don't accept a meaningless value
                Runtime.Log("Transfer() empty transfer amount or from==to");
                transfer(from, to, amount);
                return true;    // as per nep5 standard - return true when amount is 0 or from == to
            }

            if (!Runtime.CheckWitness(from))
            {
                // ensure transaction is signed properly by the owner of the tokens
                Runtime.Log("Transfer() CheckWitness failed");
                return false;
            }

            BigInteger recipientBalance = BalanceOf(to);
            BigInteger recipientAmountSubjectToVesting = TokenSale.SubjectToVestingPeriod(to);
            BigInteger senderAmountSubjectToVesting = TokenSale.SubjectToVestingPeriod(from);

            BigInteger newBalance = fromBalance - amount;
            Helpers.SetBalanceOf(from, newBalance + senderAmountSubjectToVesting);                  // remove balance from originating account
            Helpers.SetBalanceOf(to, recipientBalance + recipientAmountSubjectToVesting + amount);  // set new balance for destination account

            transfer(from, to, amount);
            return true;
        }

        /// <summary>
        /// transfer an amount from the "from" account to the "to" acount if the "spender" has been approved to transfer the requested amount.
        /// </summary>
        /// <param name="spender"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        /// <param name="caller"></param>
        /// <param name="entry"></param>
        /// <returns></returns>
        public static bool TransferFrom(byte[] spender, byte[] from, byte[] to, BigInteger amount, byte[] caller, byte[] entry)
        {
            if (caller != entry && !Helpers.IsContractWhitelistedDEX(caller))
            {
                spender = caller;
            }

            if (spender.Length != 20 || from.Length != 20 || to.Length != 20)
            {
                Runtime.Log("TransferFrom() (spender|from|to).Length != 20");
                return false;
            }

            if (amount < 0)
            {
                Runtime.Log("TransferFrom() invalid transfer amount must be >= 0");
                throw new Exception();
            }

            BigInteger approvedTransferAmount = Allowance(from, spender);    // how many tokens is this address authorised to transfer
            BigInteger fromBalance = BalanceOf(from);                   // retrieve balance of authorised account

            if (approvedTransferAmount < amount || fromBalance < amount)
            {
                // don't transfer if funds not available
                Runtime.Notify("TransferFrom() (authorisedAmount|fromBalance) < transferValue", approvedTransferAmount, fromBalance, amount);
                return false;
            }

            BigInteger senderAmountSubjectToVesting = TokenSale.SubjectToVestingPeriod(from);

            if (amount == 0 || from == to || fromBalance - senderAmountSubjectToVesting < amount)
            {
                // don't accept a meaningless value
                Runtime.Log("TransferFrom() empty transfer amount or from==to");
                transfer(from, to, amount);
                return true;    // as per nep5 standard - return true when amount is 0 or from == to
            }

            if (!Runtime.CheckWitness(spender))
            {
                // ensure transaction is signed properly by the spender
                Runtime.Log("TransferFrom() CheckWitness failed");
                return false;
            }

            BigInteger recipientBalance = BalanceOf(to);
            BigInteger recipientAmountSubjectToVesting = TokenSale.SubjectToVestingPeriod(to);

            BigInteger newBalance = fromBalance - amount;
            Helpers.SetBalanceOf(from, newBalance + senderAmountSubjectToVesting);                  // remove balance from originating account
            Helpers.SetBalanceOf(to, recipientBalance + recipientAmountSubjectToVesting + amount);  // set new balance for destination account
            Helpers.SetAllowanceAmount(from.Concat(spender), approvedTransferAmount - amount);   // deduct transferred amount from allowance

            transfer(from, to, amount);
            return true;
        }

    }
}
