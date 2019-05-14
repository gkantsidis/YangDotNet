namespace Yang.Parser.Tests

module ArgumentsTests =
    open System
    open System.Numerics
    open Xunit
    open Yang.Model.Arguments
    open Yang.Parser.Arguments

    [<Fact>]
    let ``parse length of min value`` () =
        let input = "min"
        let result = FParsecHelper.apply parse_length input
        Assert.Equal(Length [ (LengthPart.Single LengthBoundary.Min) ], result)

    [<Fact>]
    let ``parse length of max value`` () =
        let input = "max"
        let result = FParsecHelper.apply parse_length input
        Assert.Equal(Length [ (LengthPart.Single LengthBoundary.Max) ], result)

    [<Fact>]
    let ``parse length of number value`` () =
        let input = "15"
        let result = FParsecHelper.apply parse_length input
        Assert.Equal(Length [ (LengthPart.Single (LengthBoundary.Number 15uL)) ], result)

    // TODO: Add more tests for length-arg parser


    [<Fact>]
    let ``parse range of min value`` () =
        let input = "min"
        let result = FParsecHelper.apply parse_range input
        Assert.Equal(Range [ (RangePart.Single RangeBoundary.Min) ], result)

    [<Fact>]
    let ``parse range of max value`` () =
        let input = "max"
        let result = FParsecHelper.apply parse_range input
        Assert.Equal(Range [ (RangePart.Single RangeBoundary.Max) ], result)

    [<Fact>]
    let ``parse range of integer number value`` () =
        let input = "15"
        let (Range result) = FParsecHelper.apply parse_range input
        Assert.Equal(1, result.Length)
        let part = result.Head
        Assert.True(part._IsSingle)
        let single = part.AsSingle
        Assert.True(single.IsSome)
        let boundary = single.Value
        Assert.True(boundary._IsInteger)
        let value = boundary.AsInteger
        Assert.True(value.IsSome)
        Assert.Equal(15L, value.Value)

    [<Fact>]
    let ``parse range of negative integer number value`` () =
        let input = "-15"
        let (Range result) = FParsecHelper.apply parse_range input
        Assert.Equal(1, result.Length)
        let part = result.Head
        Assert.True(part._IsSingle)
        let single = part.AsSingle
        Assert.True(single.IsSome)
        let boundary = single.Value
        Assert.True(boundary._IsInteger)
        let value = boundary.AsInteger
        Assert.True(value.IsSome)
        Assert.Equal(-15L, value.Value)

    [<Fact>]
    let ``parse range of decimal number value`` () =
        let input = "3.14"
        let (Range result) = FParsecHelper.apply parse_range input
        Assert.Equal(1, result.Length)
        let part = result.Head
        Assert.True(part._IsSingle)
        let boundary = part.AsSingle
        Assert.True(boundary.IsSome)
        Assert.True(boundary.Value._IsDecimal)
        let value = boundary.Value.AsDecimal
        Assert.True(value.IsSome)
        Assert.True(System.Math.Abs(value.Value - 3.14M) < 0.01M)

    [<Fact>]
    let ``parse range of negative decimal number value`` () =
        let input = "-3.14"
        let (Range result) = FParsecHelper.apply parse_range input
        Assert.Equal(1, result.Length)
        let part = result.Head
        Assert.True(part._IsSingle)
        let boundary = part.AsSingle
        Assert.True(boundary.IsSome)
        Assert.True(boundary.Value._IsDecimal)
        let value = boundary.Value.AsDecimal
        Assert.True(value.IsSome)
        Assert.True(System.Math.Abs(value.Value + 3.14M) < 0.01M)

    [<Fact>]
    let ``parse range of set of values`` () =
        let input = "15 .. 20"
        let (Range result) = FParsecHelper.apply parse_range input
        Assert.Equal(1, result.Length)
        let region = result.Head
        Assert.True(region._IsRegion)
        let r = region.AsRegion
        Assert.True(r.IsSome)
        let (left, right) = r.Value
        Assert.True(left._IsInteger)
        Assert.True(right._IsInteger)
        let li = left.AsInteger
        let ri = right.AsInteger
        Assert.True(li.IsSome)
        Assert.True(ri.IsSome)
        Assert.Equal(15L, li.Value)
        Assert.Equal(20L, ri.Value)

    [<Fact>]
    let ``parse range with big range`` () =
        let input = "1 .. 18446744073709551615"
        let result = FParsecHelper.apply parse_range_part input
        Assert.True(result._IsRegion)
        let region = result.AsRegion
        Assert.True(region.IsSome)
        let (left, right) = region.Value
        Assert.True(left._IsInteger)
        Assert.True(right._IsInteger)
        let lefti = left.AsInteger
        let righti = right.AsBigInteger
        Assert.True(lefti.IsSome)
        Assert.True(righti.IsSome)
        Assert.Equal(1L, lefti.Value)
        Assert.Equal(BigInteger.Parse("18446744073709551615"), righti.Value)

    [<Fact>]
    let ``parse range with no space`` () =
        let input = "1..96"
        let result = FParsecHelper.apply parse_range_part input
        Assert.True(result._IsRegion)
        let region = result.AsRegion
        Assert.True(region.IsSome)
        let (left, right) = region.Value
        Assert.True(left._IsInteger)
        Assert.True(right._IsInteger)
        let lefti = left.AsInteger
        let righti = right.AsInteger
        Assert.True(lefti.IsSome)
        Assert.True(righti.IsSome)
        Assert.Equal(1L, lefti.Value)
        Assert.Equal(96L, righti.Value)

    // TODO: Add more tests for range-arg parser
    [<Theory>]
    [<InlineData("min .. max")>]
    [<InlineData("min..max")>]
    [<InlineData("min.. max")>]
    [<InlineData("min ..max")>]
    let ``parse min to max range part`` (input : string) =
        let result = FParsecHelper.apply parse_range_part input
        Assert.True(result._IsRegion)
        let region = result.AsRegion
        Assert.True(region.IsSome)
        let (left, right) = region.Value
        Assert.Equal(RangeBoundary.Min, left)
        Assert.Equal(RangeBoundary.Max, right)

    [<Theory>]
    [<InlineData("1..2",     1,   2)>]
    [<InlineData("1 .. 2",   1,   2)>]
    [<InlineData("-1..2",   -1,   2)>]
    [<InlineData("-1 .. 2", -1,   2)>]
    [<InlineData("-11..-2", -11, -2)>]
    let ``parse valid integer range part`` (input : string, left : int, right : int) =
        let result = FParsecHelper.apply parse_range_part input
        Assert.True(result._IsRegion)
        let region = result.AsRegion
        Assert.True(region.IsSome)
        let (left', right') = region.Value
        Assert.True(left'._IsInteger)
        Assert.True(right'._IsInteger)
        let lv = left'.AsInteger
        let rv = right'.AsInteger
        Assert.True(lv.IsSome)
        Assert.True(rv.IsSome)
        Assert.Equal(int64 left, lv.Value)
        Assert.Equal(int64 right, rv.Value)

    [<Theory>]
    [<InlineData("1.1..2.2",        "1.1",   "2.2")>]
    [<InlineData("1.1 .. 2.2",      "1.1",   "2.2")>]
    [<InlineData("-1.1..2.2",      "-1.1",   "2.2")>]
    [<InlineData("-1.1 .. 2.2",    "-1.1",   "2.2")>]
    [<InlineData("-11.1..-2.2",   "-11.1",  "-2.2")>]
    [<InlineData("-11.1 .. -2.2", "-11.1",  "-2.2")>]
    [<InlineData("-11.1..-2.",    "-11.1",  "-2.0")>]
    let ``parse valid decimal range part`` (input : string, left : string, right : string) =
        let result = FParsecHelper.apply parse_range_part input
        Assert.True(result._IsRegion)
        let region = result.AsRegion
        Assert.True(region.IsSome)
        let (left', right') = region.Value
        Assert.True(left'._IsDecimal)
        Assert.True(right'._IsDecimal)

        let left = System.Decimal.Parse(left)
        let right = System.Decimal.Parse(right)
        let left' = left'.AsDecimal
        let right' = right'.AsDecimal
        Assert.True(left'.IsSome)
        Assert.True(right'.IsSome)
        Assert.True(System.Math.Abs(left'.Value - left) < 0.001M)
        Assert.True(System.Math.Abs(right'.Value - right) < 0.001M)

    [<Theory>]
    [<InlineData("1.1.1")>]
    [<InlineData("2 .. 2.3.4")>]
    let ``fail parsing invalid range parts`` (input : string) =
        Assert.ThrowsAny<Exception>(
            fun _ -> FParsecHelper.apply parse_range_part input |> ignore
        )

    [<Fact>]
    let ``parse min to integer 1`` () =
        let input = "min..1"
        let result = FParsecHelper.apply parse_range_part input
        Assert.True(result._IsRegion)
        let region = result.AsRegion
        Assert.True(region.IsSome)
        let (left', right') = region.Value
        Assert.True(left'._IsMin)
        Assert.True(right'._IsInteger)
        let right' = right'.AsInteger
        Assert.True(right'.IsSome)
        Assert.Equal(1L, right'.Value)

    [<Fact>]
    let ``parse min to decimal 1.0`` () =
        let input = "min..1.0"
        let result = FParsecHelper.apply parse_range_part input
        Assert.True(result._IsRegion)
        let region = result.AsRegion
        Assert.True(region.IsSome)
        let (left', right') = region.Value
        Assert.True(left'._IsMin)
        Assert.True(right'._IsDecimal)
        let right' = right'.AsDecimal
        Assert.True(right'.IsSome)
        Assert.True(System.Math.Abs(1.0M - right'.Value) < 0.0001M)

    [<Theory>]
    [<InlineData("min..min")>]
    [<InlineData("max..max")>]
    [<InlineData("1..1")>]
    [<InlineData("1.0..1.0")>]
    let ``parse trivial ranges as single values`` (input) =
        let result = FParsecHelper.apply parse_range_part input
        Assert.True(result._IsSingle)

    [<Theory>]
    [<InlineData("2 .. 1")>]
    [<InlineData("2 .. min")>]
    [<InlineData("max .. 1")>]
    [<InlineData("2.0 .. 1")>]
    [<InlineData("2 .. 1.0")>]
    let ``fail to parse ranges with left boundary bigger than right`` (input) =
        Assert.ThrowsAny<Exception>(
            fun _ -> FParsecHelper.apply parse_range_part input |> ignore
        )
