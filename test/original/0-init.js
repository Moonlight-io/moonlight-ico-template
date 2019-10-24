const dotenv = require('dotenv');
const assert = require('assert');
const chai = require('chai');
const should = chai.should();
const expect = chai.expect;
const nep5 = require("../../lib/nep5-interface");
const Networks = require("../../config/networks");

dotenv.config({path: 'config/config.env'});

/**
 * keys.env should define the following
 * CONTRACT_ADMIN_WIF=xxxxx       // contract administrator matching ICOTemplate.InitialAdminAccount
 * WHITELIST_ADDRESS_WIF=xxxxx    // the address that will be used to purchase tokens during ICO
 * RANDOM_WIF=xxxxx               // used to send tokens to during testing
 * SPENDER_WIF=xxxxx              // person who spends someone elses token using transferFrom
 */
dotenv.config({path: 'config/keys.env'});

const _config = {
  totalSupply: 750000000,
  targetGroup: 1,
  targetUnlockBlock: 1350,
  tokenName: "Your Token Name",
  tokenSymbol: "TokenSymbol",
  tokenDecimals: 8,
  mintAmountOfNEO: 100,
  mintTokenPerNEO: 2000,
  mintExpectedBalance: 0,
  tokenTransferAmount: 5000,
  tokenTransferAmountWithDecimals: 0,
};

_config.tokenTransferAmountWithDecimals = _config.tokenTransferAmount * parseInt(1 + '0'.repeat(_config.tokenDecimals));
_config.mintExpectedBalance = _config.mintAmountOfNEO * _config.mintTokenPerNEO;

/**
 * runs before any other tests are run
 *
 * test the smart contract we want to test has been initialised
 */
before(function () {
  // load the original moonlight nep5 contract
  const contractAVM = nep5.GetAVM(process.env.CONTRACT_PATH);
  process.env.DEFAULT_CONTRACT = nep5.GetScriptHashForData(contractAVM);

  // setup the nep5 interface with a network and contract target
  nep5.Setup(Networks[process.env.DEFAULT_NETWORK], process.env.DEFAULT_CONTRACT);
  nep5.config = _config;

  // setup wallets to be used during unit tests
  nep5.accounts.master = new nep5.neonJs.wallet.Account(process.env.NEO_SINGLE_NODE_MASTER_WIF);
  nep5.accounts.contractAdmin = new nep5.neonJs.wallet.Account(process.env.CONTRACT_ADMIN_WIF);
  nep5.accounts.whitelistAccount = new nep5.neonJs.wallet.Account(process.env.WHITELIST_ADDRESS_WIF);
  nep5.accounts.spender = new nep5.neonJs.wallet.Account(process.env.SPENDER_WIF);
  nep5.accounts.random = new nep5.neonJs.wallet.Account(process.env.RANDOM_WIF);

  this.nep5 = nep5;
  this.expect = expect;

  it("contract is deployed", async function () {
    // set a timeout of 30s for initialisation process
    let response = await nep5.InvokeFunction('totalSupply');

    if (response.result.stack.length === 0) {
      // contract probably isn't deployed
      this.timeout(10000);
      await nep5.DeployContract(contractAVM, nep5.accounts.master.WIF);
      await nep5.Sleep(5000);
    } else {
      // have a response, contract must be deployed
    }
  });

  it("contract admin account has gas", async function () {
    let balance = await nep5.GetAssetBalance(nep5.accounts.contractAdmin.address);
    if (!balance.assets.GAS) {
      this.timeout(10000);
      // send contract admin gas - this amount will also be used for contract migration tests
      await nep5.TransferAsset(nep5.accounts.master, nep5.accounts.contractAdmin.address, 0, 520);
      await nep5.Sleep(5000);
      balance = await nep5.GetAssetBalance(nep5.accounts.contractAdmin.address);
    }

    balance.should.not.be.undefined;
    balance.assets.GAS.should.not.be.undefined;
  });

  it("whitelist account has neo & gas", async function () {
    let balance = await nep5.GetAssetBalance(nep5.accounts.whitelistAccount.address);
    if (!balance.assets.NEO) {
      this.timeout(10000);
      // send contract admin gas - this amount will also be used for contract migration tests
      await nep5.TransferAsset(nep5.accounts.master, nep5.accounts.whitelistAccount.address, _config.mintAmountOfNEO * 2, 50);
      await nep5.Sleep(5000);
      balance = await nep5.GetAssetBalance(nep5.accounts.whitelistAccount.address);
    }

    balance.should.not.be.undefined;
    balance.assets.GAS.should.not.be.undefined;
  });

  it("random account has gas", async function () {
    let balance = await nep5.GetAssetBalance(nep5.accounts.random.address);
    if (!balance.assets.GAS) {
      this.timeout(10000);
      // send contract admin gas - this amount will also be used for contract migration tests
      await nep5.TransferAsset(nep5.accounts.master, nep5.accounts.random.address, 0, 20);
      await nep5.Sleep(5000);
      balance = await nep5.GetAssetBalance(nep5.accounts.random.address);
    }

    balance.should.not.be.undefined;
    balance.assets.GAS.should.not.be.undefined;
  });

  it("spender account has gas", async function () {
    let balance = await nep5.GetAssetBalance(nep5.accounts.spender.address);
    if (!balance.assets.GAS) {
      this.timeout(10000);
      // send contract admin gas - this amount will also be used for contract migration tests
      await nep5.TransferAsset(nep5.accounts.master, nep5.accounts.spender.address, 0, 20);
      await nep5.Sleep(5000);
      balance = await nep5.GetAssetBalance(nep5.accounts.spender.address);
    }

    balance.should.not.be.undefined;
    balance.assets.GAS.should.not.be.undefined;
  });


  it("ensure contract is initialised", async function () {
    let response = await nep5.InvokeFunction('totalSupply');
    let _totalSupply = nep5.neon.u.fixed82num(response.result.stack[0].value === '' ? '00' : response.result.stack[0].value);

    if (_totalSupply === 0) {
      // initialise the contract by invoking InitSmartContract
      this.timeout(10000);
      await nep5.InitSmartContract(nep5.accounts.contractAdmin.WIF);
      await nep5.Sleep(5000);
    } else {
      // contract is already initialised - nothing to do
    }

    response = await nep5.InvokeFunction('totalSupply');
    _totalSupply = nep5.neon.u.fixed82num(response.result.stack[0].value === '' ? '00' : response.result.stack[0].value);
    _totalSupply.should.be.greaterThan(0);
  });
});