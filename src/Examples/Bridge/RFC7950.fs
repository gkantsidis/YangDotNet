﻿namespace Yang.Examples.RFC7950

module Bridge =
    open Yang.Provider

    let [<Literal>] model = """
    // This is the example from Sec. 4.2.2.5 of RFC 7950 (p.22-23)
    // Contents of "example-system.yang"
    module example-system {
        yang-version 1.1;
        namespace "urn:example:system";
        prefix "sys";

        organization "Example Inc.";
        contact "joe@example.com";
        description
            "The module for entities implementing the Example system.";

        revision 2007-06-09 {
            description "Initial revision.";
        }

        container system {
            leaf host-name {
                type string;
                description
                    "Hostname for this system.";
            }

            leaf-list domain-search {
                type string;
                description
                    "List of domain names to search.";
            }

            container login {
                leaf message {
                    type string;
                    description
                    "Message given at start of login session.";
                }

                list user {
                    key "name";
                    leaf name {
                        type string;
                    }
                    leaf full-name {
                        type string;
                    }
                    leaf class {
                        type string;
                    }
                }
            }
        }
    }
    """

    type Example = YangFromStringProvider<model>
