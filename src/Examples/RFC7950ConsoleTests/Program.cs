﻿using System;

namespace RFC7950ConsoleTests
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Prefix:          {0}", Yang.Examples.RFC7950.SimpleModel.Model.T.Information.Prefix);
            Console.WriteLine("Namespace:       {0}", Yang.Examples.RFC7950.SimpleModel.Model.T.Information.Namespace);
            Console.WriteLine("Description:     {0}", Yang.Examples.RFC7950.SimpleModel.Model.T.Information.Description);
            Console.WriteLine("Organization:    {0}", Yang.Examples.RFC7950.SimpleModel.Model.T.Information.Organization);
            Console.WriteLine("Contact:         {0}", Yang.Examples.RFC7950.SimpleModel.Model.T.Information.Contact);
            Console.WriteLine("Yang Version:    {0}", Yang.Examples.RFC7950.SimpleModel.Model.T.Information.YangVersion);
        }
    }
}
