﻿using ZhonTai.HttpApi;

namespace ZhonTai.Plate.Admin.Host
{
    public class Program
    {
        public static void Main(string[] args)
        {
            new HostBuilderProvider().Run<Startup>(args);
        }
    }
}