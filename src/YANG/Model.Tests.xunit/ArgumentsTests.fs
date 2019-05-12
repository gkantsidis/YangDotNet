namespace Yang.Model.Tests

module ArgumentsTests =
    open Xunit
    open Yang.Model.Arguments

    [<Theory>]
    [<InlineData("2010-10-04", 2010, 10, 04)>]
    [<InlineData("2010-01-04", 2010, 10, 04)>]
    [<InlineData("2010-01-30", 2010, 10, 04)>]
    let ``parse correct dates from string`` (str : string, year, month, day) =
        let date = Date.Make str
        Assert.Equal(year, date.Year)
        Assert.Equal(month, date.Month)
        Assert.Equal(day, date.Day)

    [<Theory>]
    [<InlineData("2010-13-04")>]
    let ``throw exception when parsing incorrect dates`` (str : string) =
        Assert.Throws<System.ArgumentException>(fun _ -> Date.Make str |> ignore)

    [<Fact>]
    let ``parse key that spans multiple lines`` () =
        let input = "source-port destination-port
       source-address destination-address"
        let (Key keys) = KeyFromString input
        Assert.Equal(4, keys.Length)
        Assert.Equal("source-port",         (List.item 0 keys).Value)
        Assert.Equal("destination-port",    (List.item 1 keys).Value)
        Assert.Equal("source-address",      (List.item 2 keys).Value)
        Assert.Equal("destination-address",      (List.item 3 keys).Value)
