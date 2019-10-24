describe("token sale setup", function () {
  it("test set group unlock block mechanism", async function () {
    this.timeout(10000);
    let response = await this.nep5.GetGroupUnlockBlock(this.nep5.config.targetGroup);
    let _foundUnlockBlock = this.nep5.neon.u.reverseHex(response.result.stack[0].value.toString());

    if (_foundUnlockBlock === '') {
      // group wasn't previously defined so set it now
      await this.nep5.SetGroupUnlockBlock(this.nep5.config.targetGroup, this.nep5.config.targetUnlockBlock, this.nep5.accounts.contractAdmin.WIF);
      return this.nep5.Sleep(5000);
    }

    // ensure the found unlock block equals config
    parseInt(_foundUnlockBlock, 16).should.equal(this.nep5.config.targetUnlockBlock);
  });

  it("test group unlock block is expected values", async function () {
    let response = await this.nep5.GetGroupUnlockBlock(this.nep5.config.targetGroup);
    let _foundUnlockBlock = this.nep5.neon.u.reverseHex(response.result.stack[0].value.toString());

    _foundUnlockBlock.should.not.be.empty;

    parseInt(_foundUnlockBlock, 16).should.be.equal(this.nep5.config.targetUnlockBlock);
  });

  it("whitelist an address for pre-sale", async function () {
    this.timeout(10000);
    // test if the address is whitelisted
    let whitelistResponse = await this.nep5.GetTokenSaleGroupNumber(this.nep5.accounts.whitelistAccount.scriptHash);
    let _foundGroup = this.nep5.neon.u.reverseHex(whitelistResponse.result.stack[0].value.toString());

    if (_foundGroup === '') {
      // the user has not been whitelisted yet
      await this.nep5.AddAddress(this.nep5.accounts.whitelistAccount.scriptHash, this.nep5.config.targetGroup, this.nep5.accounts.contractAdmin.WIF);
      return this.nep5.Sleep(5000);
    }

    // ensure the users group matches defined group
    parseInt(_foundGroup).should.equal(this.nep5.config.targetGroup);
  });

  it("test if whitelist was a success", async function () {
    // test if the address is whitelisted
    let whitelistResponse = await this.nep5.GetTokenSaleGroupNumber(this.nep5.accounts.whitelistAccount.scriptHash);
    let _foundGroup = this.nep5.neon.u.reverseHex(whitelistResponse.result.stack[0].value.toString());

    _foundGroup.should.not.be.empty;

    parseInt(_foundGroup, 16).should.be.equal(this.nep5.config.targetGroup);
  });

  it("invoke mintTokens", async function () {
    this.timeout(10000);
    let balance = await this.nep5.GetTokenBalance(this.nep5.accounts.whitelistAccount.scriptHash);

    if (balance > 0) {
      // already done a mintTokens - no need for another
      return;
    }

    const contractAddress = this.nep5.neonJs.wallet.getAddressFromScriptHash(process.env.DEFAULT_CONTRACT);
    const intent = this.nep5.neonJs.api.makeIntent({NEO: this.nep5.config.mintAmountOfNEO}, contractAddress);

    const script = this.nep5.neon.create.script({
      scriptHash: process.env.DEFAULT_CONTRACT,
      operation: "mintTokens",
      args: []
    });

    const invoke = {
      api: this.nep5._api,
      url: this.nep5._network.extra.rpcServer,
      account: this.nep5.accounts.whitelistAccount,
      intents: intent,
      script: script
    };

    await this.nep5.neon.doInvoke(invoke);
    await this.nep5.Sleep(5000);

    balance = await this.nep5.GetTokenBalance(this.nep5.accounts.whitelistAccount.scriptHash);

    balance.should.be.equal(this.nep5.config.mintExpectedBalance);
  });

  it("test nep5 token was minted", async function () {
    const balance = await this.nep5.GetTokenBalance(this.nep5.accounts.whitelistAccount.scriptHash);

    balance.should.be.greaterThan(this.nep5.config.mintTokenPerNEO);
  });
});