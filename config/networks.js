module.exports = {
  LocalNet: {
    name: "LocalNet",
    extra: {
      neoscan: "http://127.0.0.1:4000/api/main_net",
      rpcServer: "http://127.0.0.1:30333"
    }
  },
  PrivateNet: {
    name: "PrivateNet",
    extra: {
      neoscan: "http://privnet.moonlight.io:4000/api/main_net",
      rpcServer: "http://privnet.moonlight.io:30333"
    }
  },
  MainNet: {
    name: "MainNet",
    extra: {
      neoscan: "https://neoscan.io/api/main_net",
      rpcServer: "http://seed6.ngd.network:10332"
    }
  },
  TestNet: {
    name: "TestNet",
    extra: {
      neoscan: "https://neoscan-testnet.io/api/main_net",
      rpcServer: "http://seed3.ngd.network:20332"
    }
  }
};