let neonJs = require('@cityofzion/neon-js');
const fs = require("fs");

const NEP5 = {
  neonJs: neonJs,
  neon: neonJs.default,
  _network: null,
  _contract: null,
  _api: null,
  accounts: {},
  SetNetwork: function (_targetNetwork) {
    this._network = _targetNetwork;
    this.neon.add.network(this._network);
    return this;
  },
  SetContractScriptHash: function (_targetContract) {
    this._contract = _targetContract;
    return this;
  },
  SetAPIProvider: function () {
    this._api = new neonJs.api.neoscan.instance(this._network.name);
    return this;
  },
  Setup: function (_network, _contract) {
    this.SetNetwork(_network)
      .SetContractScriptHash(_contract)
      .SetAPIProvider();

    return this;
  },
  /**
   * pause execution for `ms`
   * @param ms
   * @returns {Promise<unknown>}
   * @constructor
   */
  Sleep: function (ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
  },
  /**
   *
   * @param str
   * @returns {string}
   * @constructor
   */
  HexToAscii: function (str) {
    const hex = str.toString();
    let output = '';
    for (let n = 0; n < hex.length; n += 2) {
      output += String.fromCharCode(parseInt(hex.substr(n, 2), 16));
    }
    return output;
  },
  /**
   * load the contents of an avm
   * @param avmPath
   * @constructor
   * @return {string}
   */
  GetAVM: function (avmPath) {
    return fs.readFileSync(avmPath, "hex");
  },
  /**
   * return the scriptHash for a file
   * @returns {string}
   * @constructor
   * @param data
   */
  GetScriptHashForData: function (data) {
    return this.neon.u.reverseHex(this.neon.u.hash160(data));
  },

  /**
   * get balance object for address from neoscan
   * @param address
   * @returns {Promise<Balance>}
   * @constructor
   */
  GetAssetBalance: async function (address) {
    return await this._api.getBalance(address);
  },
  /**
   * initialise the moonlight smart contract
   * @param _wif
   * @returns {*}
   * @constructor
   */
  InitSmartContract: function (_wif) {
    return this.ContractInvocation('admin', [this.neon.u.str2hexstring('InitSmartContract')], _wif)
  },
  /**
   * get the totalSupply of a nep5 contract
   * @returns {*}
   * @constructor
   */
  InvokeFunction: function (fnName) {
    const getBalance = {
      scriptHash: this._contract,
      operation: fnName,
      args: []
    };
    return this.ScriptInvocation(getBalance);
  },
  /**
   * get the token sale unlock block for specified group
   * @param _targetGroup
   * @returns {*}
   * @constructor
   */
  GetGroupUnlockBlock: function (_targetGroup) {
    const getGroupUnlockBlock = {
      scriptHash: this._contract,
      operation: 'GetGroupUnlockBlock',
      args: [_targetGroup]
    };
    return this.ScriptInvocation(getGroupUnlockBlock);
  },
  /**
   * define the block that a group can participate in the token sale
   * @param _group
   * @param _block
   * @param _wif
   * @returns {Promise<void>}
   * @constructor
   */
  SetGroupUnlockBlock: async function (_group, _block, _wif) {
    return await this.ContractInvocation('SetGroupUnlockBlock', [_group, _block], _wif)
  },
  /**
   * whitelist an address to a token sale group
   * @param _address
   * @param _group
   * @param _wif
   * @returns {*}
   * @constructor
   */
  AddAddress: function (_address, _group, _wif) {
    return this.ContractInvocation('AddAddress', [this.neon.u.reverseHex(_address), _group], _wif)
  },
  /**
   * get the token sale group number for specified address
   * @param targetAddress
   * @returns {*}
   * @constructor
   */
  GetTokenSaleGroupNumber: function (targetAddress) {
    const getGroupNumber = {
      scriptHash: this._contract,
      operation: 'GetGroupNumber',
      args: [this.neon.u.reverseHex(targetAddress)]
    };

    return this.ScriptInvocation(getGroupNumber);
  },
  /**
   * transfer neo or gas to an address
   * @param _accountFrom
   * @param _addressTo
   * @param _neoAmount
   * @param _gasAmount
   * @constructor
   */
  TransferAsset: function (_accountFrom, _addressTo, _neoAmount, _gasAmount) {
    let assets = {};

    if (_neoAmount > 0) {
      assets.NEO = _neoAmount;
    }

    if (_gasAmount > 0) {
      assets.GAS = _gasAmount;
    }

    const intent = this.neonJs.api.makeIntent(assets, _addressTo);
    const config = {
      api: this._api,
      url: this._network.extra.rpcServer,
      account: _accountFrom,
      intents: intent
    };

    return this.neon.sendAsset(config);
  },
  /**
   * deploy a contract to the neo network
   * @param avmData
   * @param _wif
   * @returns {never}
   * @constructor
   */
  DeployContract: function (avmData, _wif) {
    const n = this.neon;
    const walletAccount = new this.neonJs.wallet.Account(_wif);
    const sb = n.create.scriptBuilder();

    sb.emitPush(n.u.str2hexstring("")) // description
      .emitPush(n.u.str2hexstring("")) // email
      .emitPush(n.u.str2hexstring("")) // author
      .emitPush(n.u.str2hexstring("")) // code_version
      .emitPush(n.u.str2hexstring("")) // name
      .emitPush(0x01) // storage: {none: 0x00, storage: 0x01, dynamic: 0x02, storage+dynamic:0x03}
      .emitPush("05") // expects hexstring  (_emitString) // usually '05'
      .emitPush("0710") // expects hexstring  (_emitString) // usually '0710'
      .emitPush(avmData) //script
      .emitSysCall('Neo.Contract.Create');

    const network = {
      api: this._api,
      url: this._network.extra.rpcServer,
      account: walletAccount,
      script: sb.str,
      fees: 1,
      gas: 490
    };

    return n.doInvoke(network);
  },
  /**
   * get the nep5 token balance associated with address
   * @param _address
   * @returns {*}
   * @constructor
   */
  BalanceOf: function (_address) {
    const getBalance = {
      scriptHash: this._contract,
      operation: 'balanceOf',
      args: [this.neon.u.reverseHex(_address)]
    };

    return this.ScriptInvocation(getBalance);
  },
  /**
   * wrapper method for BalanceOf but formats return to fixed8
   * @param _address
   * @returns {Promise<number>}
   * @constructor
   */
  GetTokenBalance: async function (_address) {
    let response = await this.BalanceOf(_address);
    return this.neon.u.fixed82num(response.result.stack[0].value === '' ? '00' : response.result.stack[0].value);
  },
  /**
   * test the balance of an address subject to vesting
   * @param scriptHash
   * @param callback
   * @constructor
   */
  BalanceOfVestedAddress: function (scriptHash, callback) {
    const getVestedBalance = {
      scriptHash: this._contract,
      operation: 'BalanceOfVestedAddress',
      args: [this.neon.u.reverseHex(scriptHash)]
    };
    return this.ScriptInvocation(getVestedBalance);
  },
  /**
   * transfer a nep5 token from one account to another
   * @param _addressFromScriptHash
   * @param _addressToScriptHash
   * @param _amount
   * @param _wif
   * @returns {*}
   * @constructor
   */
  Transfer: function (_addressFromScriptHash, _addressToScriptHash, _amount, _wif) {
    return this.ContractInvocation('transfer', [this.neon.u.reverseHex(_addressFromScriptHash), this.neon.u.reverseHex(_addressToScriptHash), _amount], _wif)
  },
  /**
   * transfer a nep5 token from one address to another using transferFrom method
   * @param _spenderAddressScriptHash
   * @param _addressFromScriptHash
   * @param _addressToScriptHash
   * @param _amount
   * @param _wif
   * @returns {*|never}
   * @constructor
   */
  TransferFrom: function (_spenderAddressScriptHash, _addressFromScriptHash, _addressToScriptHash, _amount, _wif) {
    return this.ContractInvocation('transferFrom', [
      this.neon.u.reverseHex(_spenderAddressScriptHash),
      this.neon.u.reverseHex(_addressFromScriptHash),
      this.neon.u.reverseHex(_addressToScriptHash),
      _amount
    ], _wif)
  },
  /**
   * approve spender to transfer up to amount from 'from' account
   * @param _addressFromScriptHash
   * @param _spenderAddressScriptHash
   * @param _amount
   * @param _wif
   * @returns {*|never}
   * @constructor
   */
  Approve: function (_addressFromScriptHash, _spenderAddressScriptHash, _amount, _wif) {
    return this.ContractInvocation('approve', [
      this.neon.u.reverseHex(_addressFromScriptHash),
      this.neon.u.reverseHex(_spenderAddressScriptHash),
      _amount
    ], _wif)
  },
  /**
   * determine how much spender is allowed to transfer
   * @param _addressFromScriptHash
   * @param _spenderAddressScriptHash
   * @returns {*|Promise<any>}
   * @constructor
   */
  Allowance: function (_addressFromScriptHash, _spenderAddressScriptHash) {
    const allowance = {
      scriptHash: this._contract,
      operation: 'allowance',
      args: [
        this.neon.u.reverseHex(_addressFromScriptHash),
        this.neon.u.reverseHex(_spenderAddressScriptHash)
      ]
    };

    return this.ScriptInvocation(allowance);
  },
  /**
   * wrapper method for ALlowance but formats result to fixed8
   * @param _fromScriptHash
   * @param _spenderScriptHash
   * @returns {Promise<number>}
   * @constructor
   */
  GetAllowanceBalance: async function (_fromScriptHash, _spenderScriptHash) {
    let response = await this.Allowance(_fromScriptHash, _spenderScriptHash);
    return this.neon.u.fixed82num(response.result.stack[0].value === '' ? '00' : response.result.stack[0].value);
  },
  /**
   * initiate a read-only event to the rpc server
   * @param _scripts
   * @returns {Promise<any>}
   * @constructor
   */
  ScriptInvocation: function (_scripts) {
    return this.neonJs.rpc.Query.invokeScript(this.neon.create.script(_scripts))
      .execute(this._network.extra.rpcServer);
  },
  /**
   * initiate a contract invocation
   * @param _operation
   * @param _args
   * @param _wif
   * @param _gas
   * @param _fee
   * @returns {never}
   * @constructor
   */
  ContractInvocation: function (_operation, _args, _wif, _gas = 0, _fee = 0.01) {
    let walletAccount = new this.neonJs.wallet.Account(_wif);

    const invoke = {
      api: this._api,
      url: this._network.extra.rpcServer,
      script: this.neon.create.script({
        scriptHash: this._contract,
        operation: _operation,
        args: _args
      }),
      account: walletAccount,
      gas: _gas,
      fees: _fee
    };

    return this.neon.doInvoke(invoke);
  },
  /**
   * migrate existing contract using Contract.Migrate admin method
   * @param _scriptPayload
   * @param _wif
   * @returns {*|never}
   * @constructor
   */
  ContractMigrate: function (_scriptPayload, _wif) {
    return this.ContractInvocation('admin', [
        this.neon.u.str2hexstring("ContractMigrate"),      // ...
        _scriptPayload,                      // contract upgraded bytecode
        '0710',                             // contract parameter types
        '05',                               // contract return type
        this.neon.u.int2hex(1),                   // need storage
        this.neon.u.str2hexstring('Moonlight LX contract'), // name of contract
        this.neon.u.str2hexstring('1.5'),                   // version
        this.neon.u.str2hexstring('Moonlight'),             // author
        this.neon.u.str2hexstring('chris@moonlight.io'),    // email
        this.neon.u.str2hexstring('Moonlight')              // description
      ],
      _wif,
      500,
      1);
  },
};

module.exports = NEP5;