describe('nep5 token info', function () {
  it("totalSupply() matches config", async function () {
    let response = await this.nep5.InvokeFunction('totalSupply');
    let _totalSupply = this.nep5.neon.u.fixed82num(response.result.stack[0].value === '' ? '00' : response.result.stack[0].value);
    this.expect(_totalSupply).to.be.at.least(this.nep5.config.totalSupply);
  });

  it('name() matches config', async function () {
    let response = await this.nep5.InvokeFunction("name");
    this.nep5.config.tokenName.should.equal(this.nep5.HexToAscii(response.result.stack[0].value));
  });

  it('symbol() matches config', async function () {
    let response = await this.nep5.InvokeFunction("symbol");
    this.nep5.config.tokenSymbol.should.equal(this.nep5.HexToAscii(response.result.stack[0].value));
  });

  it('decimals() matches config', async function () {
    let response = await this.nep5.InvokeFunction("decimals");
    this.nep5.config.tokenDecimals.should.equal(parseInt(response.result.stack[0].value.toString(), 16));
  });

  it('test balanceOfVestedAddress', async function () {
    let response = await this.nep5.BalanceOfVestedAddress(this.nep5.neonJs.wallet.getScriptHashFromAddress(process.env.VESTED_ADDRESS));
    const balance = this.nep5.neon.u.fixed82num(response.result.stack[0].value === '' ? '00' : response.result.stack[0].value);
    balance.should.be.greaterThan(0);
  });
});