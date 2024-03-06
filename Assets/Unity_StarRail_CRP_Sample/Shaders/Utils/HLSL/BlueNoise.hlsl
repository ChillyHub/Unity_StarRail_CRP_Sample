#ifndef CRP_BLUE_NOISE_INCLUDED
#define CRP_BLUE_NOISE_INCLUDED

// https://www.shadertoy.com/view/3tB3z3
// -------------------------------------

uint Part1By1 (uint x) {
    x = (x & 0x0000ffffu);
    x = ((x ^ (x << 8u)) & 0x00ff00ffu);
    x = ((x ^ (x << 4u)) & 0x0f0f0f0fu);
    x = ((x ^ (x << 2u)) & 0x33333333u);
    x = ((x ^ (x << 1u)) & 0x55555555u);
    return x;
}
    
uint Compact1By1 (uint x) {
    x = (x & 0x55555555u);
    x = ((x ^ (x >> 1u)) & 0x33333333u);
    x = ((x ^ (x >> 2u)) & 0x0f0f0f0fu);
    x = ((x ^ (x >> 4u)) & 0x00ff00ffu);
    x = ((x ^ (x >> 8u)) & 0x0000ffffu);
    return x;
}
    
uint PackMorton2x16(float2 v) {
	return Part1By1(v.x) | (Part1By1(v.y) << 1);
}

float2 UnpackMorton2x16(uint p) {
    return float2(Compact1By1(p), Compact1By1(p >> 1));
}

uint InverseGray32(uint n) {
    n = n ^ (n >> 1);
    n = n ^ (n >> 2);
    n = n ^ (n >> 4);
    n = n ^ (n >> 8);
    n = n ^ (n >> 16);
    return n;
}

// https://www.shadertoy.com/view/llGcDm
int Hilbert(int2 p, int level)
{
    int d = 0;
    for (int k = 0; k < level; k++)
    {
        int n = level - k - 1;
        int2 r = (p >> n) & 1;
        d += ((3 * r.x) ^ r.y) << (2 * n);
    	if (r.y == 0)
    	{
    	    if (r.x == 1)
    	    {
    	        p = (1 << n) - 1 - p;
    	    }
    	    p = p.yx;
    	}
    }
    return d;
}

// https://www.shadertoy.com/view/llGcDm
int2 Ihilbert(int i, int level)
{
    int2 p = int2(0, 0);
    for (int k = 0; k < level; k++)
    {
        int2 r = int2((i >> 1), (i ^ (i >> 1))) & 1;
        if (r.y == 0)
        {
            if (r.x == 1)
            {
                p = (1 << k) - 1 - p;
            }
            p = p.yx;
        }
        p += r << k;
        i >>= 2;
    }
    return p;
}

// knuth's multiplicative hash function (fixed point R1)
uint Kmhf(uint x) {
    return 0x80000000u + 2654435789u * x;
}

uint KmhfInv(uint x) {
    return (x - 0x80000000u) * 827988741u;
}

// mapping each pixel to a hilbert curve index, then taking a value from the Roberts R1 quasirandom sequence for it
uint HilbertR1BlueNoise(uint2 p) {
    #if 1
    uint x = uint(Hilbert(int2(p), 17)) % (1u << 17u);
    #else
    //p = p ^ (p >> 1);
    uint x = PackMorton2x16( p ) % (1u << 17u);    
    //x = x ^ (x >> 1);
    x = InverseGray32(x);
    #endif
    #if 0
    // based on http://extremelearning.com.au/unreasonable-effectiveness-of-quasirandom-sequences/
    const float phi = 2.0 / (sqrt(5.0) + 1.0);
	return frac(0.5 + phi * float(x));
    #else
    x = Kmhf(x);
    return x;
    #endif
}

// mapping each pixel to a hilbert curve index, then taking a value from the Roberts R1 quasirandom sequence for it
float HilbertR1BlueNoiseFloat(uint2 p) {
    uint x = HilbertR1BlueNoise(p);
    #if 0
    return float(x >> 24) / 256.0;
    #else
    return float(x) / 4294967296.0;
    #endif
}

// inverse
uint2 HilbertR1BlueNoiseInv(uint x) {
    x = KmhfInv(x);
    return uint2(Ihilbert(int(x), 17));
}

#endif
