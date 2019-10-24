describe('nep5 token interaction', function () {
  it("transfer() executes", async function () {
    const accounts = this.nep5.accounts;
    let balance = await this.nep5.GetTokenBalance(accounts.random.scriptHash);

    if (balance <= 100) {
      this.timeout(10000);
      await this.nep5.Transfer(accounts.whitelistAccount.scriptHash, accounts.random.scriptHash, this.nep5.config.tokenTransferAmountWithDecimals, accounts.whitelistAccount.WIF);
      await this.nep5.Sleep(5000);

      balance = await this.nep5.GetTokenBalance(this.nep5.accounts.random.scriptHash);

      // this was just minted so amount must match exactly
      balance.should.be.at.least(this.nep5.config.tokenTransferAmount);
    }

    // if this test is being run multiple times - settle for simply having some kind of balance
    balance.should.be.greaterThan(100);
  });

  it("transferFrom() executes and fails", async function () {
    const accounts = this.nep5.accounts;
    let balance = await this.nep5.GetTokenBalance(accounts.random.scriptHash);

    if (balance === this.nep5.config.tokenTransferAmount) {
      // test the transferFrom method
      this.timeout(10000);
      await this.nep5.TransferFrom(accounts.spender.scriptHash, accounts.random.scriptHash, accounts.contractAdmin.scriptHash, 100000000000, accounts.spender.WIF);
      await this.nep5.Sleep(5000);
      let balance = await this.nep5.GetTokenBalance(accounts.contractAdmin.scriptHash);
      balance.should.be.equal(0);
    }
  });

  it("approve() executes", async function () {
    const accounts = this.nep5.accounts;
    let balance = await this.nep5.GetTokenBalance(accounts.random.scriptHash);

    if (balance === this.nep5.config.tokenTransferAmount) {
      // test the approve method
      this.timeout(10000);
      await this.nep5.Approve(accounts.random.scriptHash, accounts.spender.scriptHash, 100000000000, accounts.random.WIF);
      await this.nep5.Sleep(5000);
      let allowance = await this.nep5.GetAllowanceBalance(accounts.random.scriptHash, accounts.spender.scriptHash);
      allowance.should.be.equal(1000);
    }
  });

  it("transferFrom() executes and succeeds", async function () {
    const accounts = this.nep5.accounts;
    let balance = await this.nep5.GetTokenBalance(accounts.random.scriptHash);
    let origAllowance = await this.nep5.GetAllowanceBalance(accounts.random.scriptHash, accounts.spender.scriptHash);

    if (balance > 0 && origAllowance > 0) {
      // test the transferFrom method
      this.timeout(15000);
      await this.nep5.TransferFrom(accounts.spender.scriptHash, accounts.random.scriptHash, accounts.contractAdmin.scriptHash, 700000000, accounts.spender.WIF);
      await this.nep5.Sleep(5000);
      let balance = await this.nep5.GetTokenBalance(accounts.random.scriptHash);
      let recipientBalance = await this.nep5.GetTokenBalance(accounts.contractAdmin.scriptHash);
      let allowance = await this.nep5.GetAllowanceBalance(accounts.random.scriptHash, accounts.spender.scriptHash);

      recipientBalance.should.be.greaterThan(0);
      allowance.should.be.lessThan(origAllowance);
    }
  });
});