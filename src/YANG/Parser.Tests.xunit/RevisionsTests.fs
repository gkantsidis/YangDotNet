﻿namespace Yang.Parser.Tests

module RevisionsTests =
    open Xunit
    open Yang.Model
    open Yang.Parser
    open Yang.Parser.Revisions

    [<Fact>]
    let ``revision info with no description`` () =
        let revision = """revision 2007-06-09;"""

        let revision = FParsecHelper.apply parse_revision revision
        let date, _ = revision

        Assert.Equal(2007us,    date.Year)
        Assert.Equal(6uy,       date.Month)
        Assert.Equal(9uy,       date.Day)
        Assert.Equal(None,      RevisionStatement.Description   revision)
        Assert.Equal(None,      RevisionStatement.Reference     revision)
        Assert.Equal(None,      RevisionStatement.Unknown       revision)

    [<Fact>]
    let ``simple revision info with description`` () =
        let revision = """revision 2007-06-09 {
            description "Initial revision.";
        }
"""

        let revision = FParsecHelper.apply parse_revision revision
        let date, _ = revision

        Assert.Equal(2007us,    date.Year)
        Assert.Equal(6uy,       date.Month)
        Assert.Equal(9uy,       date.Day)
        Assert.Equal(None,      RevisionStatement.Unknown   revision)
        Assert.Equal(None,      RevisionStatement.Reference revision)
        Assert.Equal(Some ("Initial revision.", None), RevisionStatement.Description revision)

    [<Fact>]
    let ``revision info with description with options`` () =
        let revision = """revision 2007-06-09 {
    description "Initial revision." {
        ex:documentation-flag 5;
    }
}
"""

        let revision = FParsecHelper.apply parse_revision revision
        let date, _ = revision

        Assert.Equal(2007us,    date.Year)
        Assert.Equal(6uy,       date.Month)
        Assert.Equal(9uy,       date.Day)
        Assert.Equal(None,      RevisionStatement.Unknown   revision)
        Assert.Equal(None,      RevisionStatement.Reference revision)

        Assert.Equal(
            (   "InitialRevision",
                Some [
                    Statement.Unknown (
                        IdentifierWithPrefix.Make "ex:documentation-flag", 
                        Some "5",
                        None
                    )
                ]
            ) |> Option.Some,
            RevisionStatement.Description revision
        )

    // TODO: Add unit test for revision with non-empty reference
    // TODO: Add unit test for revision with unknown statements
    // TODO: Add unit test for revision with description and extra body
    // TODO: Add unit test for revision with non-empty reference and extra body
