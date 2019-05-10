// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

#load "Client.fs"

open System
open Yang.NetConf

// Define your library scripting code here

let machine = Environment.GetEnvironmentVariable("NETCONF_TEST_MACHINE")
let user = Environment.GetEnvironmentVariable("NETCONF_TEST_USER")
let password = Environment.GetEnvironmentVariable("NETCONF_TEST_PASSWORD")


