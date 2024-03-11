# Nix shell script to build a reproducible environment for running
# Microsoft C# .NET applications.
#
# Invoke with `nix-shell`
#
# Test with `dotnet --info`
#

{ pkgs ? import <nixpkgs> {} }:

pkgs.mkShell {
  buildInputs = [
    pkgs.dotnet-sdk_8
  ];

  shellHook = ''
    echo "Environment set up with .NET SDK."
  '';
}
