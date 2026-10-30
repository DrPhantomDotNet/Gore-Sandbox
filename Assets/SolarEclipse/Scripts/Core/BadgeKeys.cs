namespace SolarEclipse
{
    /// <summary>
    /// Badge signing keys. Only the PUBLIC key is in clear. The private key is stored encrypted (AES-256-CBC, key from
    /// PBKDF2-HMAC-SHA256 with 200000 iterations and a random salt over the dev login password); it is decrypted in memory
    /// after a successful dev login and never saved. Generated offline; regenerate both values together.
    /// </summary>
    static class BadgeKeys
    {
        public const string PublicModulus = "0VOSLn6J1tXpAmheH5II7WmXjzpVmYFnu+0Gb3cWcnWpz11rcO9ltJV0qiy/1MTjCzVXK5IJeYTd7pueEXyXRSHMo7Nl6NfVRAbeEml39I7d+r8hE2XmOP79Ny9ZktwjnIs1akzfQBgiapWeVpflhjWJsAQDhgk3oN2ofjE3EBRFqflPO5ARKe77w7SaHYxiIoZgXJ21YMnTfMO5nndnC983xs7QBuS0k7dICWz1SPB+dWBw5CEYIUfJGyCj6H6NSR2nvvxldcx+Kum5vR0mA/IJVMSaOdVxJdtH/0x5MVWAddpJwozfEdLnPAIjzexck/OBsqslMGA4w+0U+NsNww==";
        public const string PublicExponent = "AQAB";
        /// <summary>salt(16) | iv(16) | AES-256-CBC ciphertext of the private key.</summary>
        public const string EncryptedPrivateKey = "5ZMXMN0a84VP3DSBP2KSZy4rvmT7cApRx3XQGTIgErvKAtsoKgs90jftRTX7+BEe3ZDlKEjlxcOXYl6jIbsA3YUB9Nvo8CtOILxmDNfvf3gZ0Gnce0trZIqhL+C6F6NJR+lN9kuhPlfL8oYj9Zwn+at5H3HBFV9HEwJbWCoK+K3Qm4fr8eL1VN8HhODQ8ay5ZIyZfISDIz/6Id9/cq8QnykGzzqSdfwl+/t/cAZUe2Xspd45TnsbnUq7o1W0KPqO1xDtjVMvlU5mKlyg5h7Rj1ikdDCvbOYs/U/QHmYTo5Y4J1uriTqc5tG29t3OL/u55sNEXuWum1O8nifELVT5c+kAE65SaphbDLZ6g/VXyRKxdOdP9lpMQhJH446mbVI6mge7B7al8YcbSb0eM5LktouIQ2iVRc18Kz0hVKAch20IIeKNF2mfW7ogOcBulOVWtW3PJ8DJK35YvdVW2USc2SnEEYDwzCodJs3r4cEtRd98ZQJG77n+HKZOhNV9ehQnUiK5TeKhc0VrHYoIiGEIfBB5TfpzHxBKYcGli+ak9A5W5mDOlTtbOISYc8zgpk79jWCrvvikHgE3xduKfgkypPR5N/MLle+HmaJcG04MeJS0F4qS7q9X5tPq7xO+CP5YGL74UBVVQBUBXQxcI/NHRoVVAXf85HQZEVDjNK/zM9SC/fxuy8o/f0PNVvrTyLOdOGIYn7UAxz6ByiCuxFrNGAYo1nNBAninqqygTOCWjYkgEamZxI0CDhIK+sW7W5KYc8CeuG9txkUApyABP0G3TkbvZsJLJuan0hZXl+Ys3Rg41nYRqIQR1qxjj7EOk9tolS6bwH8UV6JbUVC4PI1qoiaz84sh0I86SWsKx6WX4eS0IhaqPJw1b1wp1itroGj5CbflDevSspYoN8nwyKJgCFS6OZy4ySTmp38hnWIj7iDnT6URZ05TNID9MvX6V6RtiT/mwIQ9HNLwOjMpNLpJO9aq+y/TCofTDte0WylRSXBKaB6Segi8/iUNp1p128/Q//I4huW2j4L1YlQITjQAj4YpH+kr7910W2iaOscSjDXefROxRPGXI0LA2mPDIkImzUld2mcpNiqdfgYFsIkLRPOr2o+iQYr6MS5Xw4K1pVwmOsKKR+gmSXc3i7j3glPeVsW1ZdpgU+Crf2iuuzjGmLC7eBWRGvctdIhnPNrI3L7/HIKIqHrySCi00WuwHNBpjFzerlrlfDM5lMmcPnYkoauf9iWZw1orL9CR2S1+OnW31wxDWJ2if4KtRyXyAxpxS8Gw49NiKtbgBHWNy6BYXfEJ9S6/4UNWIXmkCZtvJxpvROF7ZFunD0/tO7NeXi2aDm7uZR73QU3bvD2rqSwUlaAD15mPNZlj9YpdftAj3fqkF99ZE1pQDlhgRYierc1D8FxqwdxiGPpMmVh/t+t7EaUM9PZTlIBlru/SRdPXC4fpHyP1nhWKeJBa4rMOzAdC1Fr9cN3E4pDUmk/gQfiwLTIl8a8tX0IHWoNzi1VTUgQ1uK3WGwF72O3iYOIEPR/QXgjoY89DyFgbHfZCoiT7KqJCdliwIVxQwjlhe8pjL4uLHQsoBtqDUq0QR5zr1KmEmEqLBHoLm3rAfvIVbA2HnA==";
    }
}
