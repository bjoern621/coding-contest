{
  description = "C# / .NET development shell (VS Code + JetBrains Rider)";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = { self, nixpkgs, flake-utils }:
    flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = import nixpkgs { inherit system; };

        # SDK + runtimes merged into one dotnet root, so `dotnet --list-sdks`
        # and `dotnet --list-runtimes` both see everything.
        dotnet = with pkgs.dotnetCorePackages; combinePackages [
          sdk_10_0
          runtime_10_0
          aspnetcore_10_0
        ];

        dotnetRoot = "${dotnet}/share/dotnet";
      in
      {
        devShells.default = pkgs.mkShell {
          packages = [
            dotnet
            pkgs.netcoredbg # open-source debugger (VS Code / CLI)
            (pkgs.python3.withPackages (ps: [ ps.ortools ]))
            pkgs.icu
            pkgs.openssl
          ];

          DOTNET_ROOT = dotnetRoot;
          DOTNET_CLI_TELEMETRY_OPTOUT = "1";
          DOTNET_NOLOGO = "1";
          DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1";

          # Native libs needed by tools/extensions that ship their own binaries.
          LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath [
            pkgs.icu
            pkgs.openssl
            pkgs.zlib
            pkgs.stdenv.cc.cc.lib
          ];

          shellHook = ''
            # Global tools (`dotnet tool install -g ...`) land here.
            export PATH="$HOME/.dotnet/tools:$PATH"

            # Stable path for IDEs: ./.dotnet always points at the current SDK.
            ln -sfn ${dotnetRoot} .dotnet

            echo "🟣 .NET $(dotnet --version) ready  (DOTNET_ROOT=$DOTNET_ROOT)"
          '';
        };
      });
}
