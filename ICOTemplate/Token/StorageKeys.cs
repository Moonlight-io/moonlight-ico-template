namespace Neo.SmartContract
{
    /// <summary>
    /// helper class for strings
    /// </summary>
    public class StorageKeys
    {
        public static string ContractAdmin() => "ContractAdminKey";
        public static string BalancePrefix() => "BalancePrefix_";
        public static string ContractInitTime() => "ContractInitTime";
        public static string ContributionBalancePrefix() => "ContributionBalancePrefix_";
        public static string FounderTokenUnlockRound() => "FounderTokenUnlockRound_";
        public static string GroupContributionAmountPrefix() => "GroupContributionAmountPrefix_";
        public static string GroupUnlockPrefix() => "GroupUnlockPrefix_";
        public static string KYCWhitelistPrefix() => "KYCWhitelistApproved";
        public static string MintTokensLastTX() => "lastMintTokensTXHash";
        public static string PresaleAllocatedValue() => "PresaleAllocatedValue";
        public static string PresaleAllocationLocked() => "PresaleAllocationLocked";
        public static string TokenTotalSupply() => "TokenTotalSupply";
        public static string TransferAllowancePrefix() => "TransferAllowancePrefix_";
        public static string VestedBalancePrefix() => "VestedBalancePrefix_";
        public static string VestedTokenPrefix() => "VestedTokenPrefix_";
        public static string WhiteListedDEXList() => "WhiteListedDEXList";
        public static string WhiteListDEXSettingChecked() => "WhiteListDEXSettingChecked";
    }
}
