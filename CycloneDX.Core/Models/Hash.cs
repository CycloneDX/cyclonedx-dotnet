// This file is part of the CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Copyright (c) Steve Springett. All Rights Reserved.

namespace CycloneDX.Models
{
    public enum Algorithm
    {
        MD5,
        SHA1,
        SHA_256,
        SHA_384,
        SHA_512,
        SHA3_256,
        SHA3_512,
    }

    public class Hash
    {
        public Algorithm algorithm { get; }
        public string value { get; }

        public Hash(Algorithm algorithm, string value) {
            this.algorithm = algorithm;
            this.value = value;
        }
    }

    public static class AlgorithmExtensions
    {
        public static string GetXmlString(this Algorithm algorithm)
        {
            switch (algorithm) {
                case Algorithm.MD5:
                    return "MD5";
                case Algorithm.SHA1:
                    return "SHA-1";
                case Algorithm.SHA_256:
                    return "SHA-256";
                case Algorithm.SHA_384:
                    return "SHA-384";
                case Algorithm.SHA_512:
                    return "SHA-512";
                case Algorithm.SHA3_256:
                    return "SHA3-256";
                case Algorithm.SHA3_512:
                    return "SHA3-512";
                default:
                    return "UNSUPPORTED";
            }
        }
    }
}
