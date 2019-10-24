<p align="center">
  <img
    src="https://assets.moonlight.io/vi/moonlight-logo-dark-800w.png" 
    width="400px"
    alt="Moonlight">
</p>

<p align="center" style="font-size: 48px;">
  <strong>Moonlight ICO Template</strong>
</p>

<p align="center">
 A C# ICO Template for the NEO Ecosystem
</p>

# Overview
The Moonlight team is proud to provide a new ICO template for use by the NEO community.  The template is written in C# and represents a feature-rich platform for token sales on the Neo blockchain.  We welcome pull request and issue submission.

## Features
* All NEP-5 Methods Including allowance, transferFrom, and approve
* Purchase of tokens with both NEO and GAS
* Presale methods which support tiered vesting without blocking the accounts from additional purchase
* KYC whitelisting and participation groups with variable participation blockHeight and allocations
* Immediate token minting upon purchase
* Multi-Stage vesting for founders allocation
* Contract Migration
* Presale allocation locking
* Partial refunds at hardcap
* Vested project token allocation

# Quickstart
1. Ensure that you correctly set `InitialAdminAccount` to your own address
2. [Build](http://docs.neo.org/en-us/sc/quickstart/getting-started-csharp.html) and [deploy](http://docs.neo.org/en-us/sc/quickstart/deploy-invoke.html) the contract with input params: `07`
2. Upon deployment you need to run the InitSmartContract administration method (can only be run once)
    * `main("admin", ["InitSmartContract"])`
    * **Note:** This is a latching method and cannot be undone
3. Allocate presale purchase amounts
    * `main("admin", ["AllocatePresalePurchase", hash160Recipient, tokenAmountFactorised]`
4. Lock future presale allocation
    * `main("admin", ["LockPresaleAllocation"])`
    * **Note:** This is a latching method and cannot be undone
5. Public method to ensure transparent use of `AllocatePresalePurchase`
    * `main("IsPresaleAllocationLocked", [])`

# Wallet Integration
- Check if a user has been whitelisted
    - > bool main("crowdsale_status", (hash160)address)
- Get the participation group number for the whitelisted user
    - > int main("GetGroupNumber", (hash160)address)
- Get the max number of LX the group can purchase
    - > int main("GetGroupMaxContribution")
- Get the block number the group can start participating
    - > int main("GetGroupUnlockBlock")
- Determine if the specified group number can now purchase tokens
    - > bool main("GroupParticipationIsUnlocked", (int)groupNumber)
* **Note:** `Group` refers to the phase of the sale a user can participate in.
