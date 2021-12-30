#define SWAP4(x) as_uint(as_uchar4(x).wzyx)

__constant uint2 const Keccak_f1600_RC[24] = {
	(uint2)(0x00000001, 0x00000000),
	(uint2)(0x00008082, 0x00000000),
	(uint2)(0x0000808a, 0x80000000),
	(uint2)(0x80008000, 0x80000000),
	(uint2)(0x0000808b, 0x00000000),
	(uint2)(0x80000001, 0x00000000),
	(uint2)(0x80008081, 0x80000000),
	(uint2)(0x00008009, 0x80000000),
	(uint2)(0x0000008a, 0x00000000),
	(uint2)(0x00000088, 0x00000000),
	(uint2)(0x80008009, 0x00000000),
	(uint2)(0x8000000a, 0x00000000),
	(uint2)(0x8000808b, 0x00000000),
	(uint2)(0x0000008b, 0x80000000),
	(uint2)(0x00008089, 0x80000000),
	(uint2)(0x00008003, 0x80000000),
	(uint2)(0x00008002, 0x80000000),
	(uint2)(0x00000080, 0x80000000),
	(uint2)(0x0000800a, 0x00000000),
	(uint2)(0x8000000a, 0x80000000),
	(uint2)(0x80008081, 0x80000000),
	(uint2)(0x00008080, 0x80000000),
	(uint2)(0x80000001, 0x00000000),
	(uint2)(0x80008008, 0x80000000),
};

#if PLATFORM == OPENCL_PLATFORM_NVIDIA && COMPUTE >= 35
static uint2 ROL2(const uint2 a, const int offset) {
	uint2 result;
	if (offset >= 32) {
		asm("shf.l.wrap.b32 %0, %1, %2, %3;" : "=r"(result.x) : "r"(a.x), "r"(a.y), "r"(offset));
		asm("shf.l.wrap.b32 %0, %1, %2, %3;" : "=r"(result.y) : "r"(a.y), "r"(a.x), "r"(offset));
	}
	else {
		asm("shf.l.wrap.b32 %0, %1, %2, %3;" : "=r"(result.x) : "r"(a.y), "r"(a.x), "r"(offset));
		asm("shf.l.wrap.b32 %0, %1, %2, %3;" : "=r"(result.y) : "r"(a.x), "r"(a.y), "r"(offset));
	}
	return result;
}
#elif PLATFORM == OPENCL_PLATFORM_AMD
#pragma OPENCL EXTENSION cl_amd_media_ops : enable
static uint2 ROL2(const uint2 vv, const int r)
{
	if (r <= 32)
	{
		return amd_bitalign((vv).xy, (vv).yx, 32 - r);
	}
	else
	{
		return amd_bitalign((vv).yx, (vv).xy, 64 - r);
	}
}
#else
static uint2 ROL2(const uint2 v, const int n)
{
	uint2 result;
	if (n <= 32)
	{
		result.y = ((v.y << (n)) | (v.x >> (32 - n)));
		result.x = ((v.x << (n)) | (v.y >> (32 - n)));
	}
	else
	{
		result.y = ((v.x << (n - 32)) | (v.y >> (64 - n)));
		result.x = ((v.y << (n - 32)) | (v.x >> (64 - n)));
	}
	return result;
}
#endif

static void chi(uint2 * a, const uint n, const uint2 * t)
{
	a[n+0] = bitselect(t[n + 0] ^ t[n + 2], t[n + 0], t[n + 1]);
	a[n+1] = bitselect(t[n + 1] ^ t[n + 3], t[n + 1], t[n + 2]);
	a[n+2] = bitselect(t[n + 2] ^ t[n + 4], t[n + 2], t[n + 3]);
	a[n+3] = bitselect(t[n + 3] ^ t[n + 0], t[n + 3], t[n + 4]);
	a[n+4] = bitselect(t[n + 4] ^ t[n + 1], t[n + 4], t[n + 0]);
}

static void keccak_f1600_round(uint2* a, uint r)
{
	uint2 t[25];
	uint2 u;

	// Theta
	t[0] = a[0] ^ a[5] ^ a[10] ^ a[15] ^ a[20];
	t[1] = a[1] ^ a[6] ^ a[11] ^ a[16] ^ a[21];
	t[2] = a[2] ^ a[7] ^ a[12] ^ a[17] ^ a[22];
	t[3] = a[3] ^ a[8] ^ a[13] ^ a[18] ^ a[23];
	t[4] = a[4] ^ a[9] ^ a[14] ^ a[19] ^ a[24];
	u = t[4] ^ ROL2(t[1], 1);
	a[0] ^= u;
	a[5] ^= u;
	a[10] ^= u;
	a[15] ^= u;
	a[20] ^= u;
	u = t[0] ^ ROL2(t[2], 1);
	a[1] ^= u;
	a[6] ^= u;
	a[11] ^= u;
	a[16] ^= u;
	a[21] ^= u;
	u = t[1] ^ ROL2(t[3], 1);
	a[2] ^= u;
	a[7] ^= u;
	a[12] ^= u;
	a[17] ^= u;
	a[22] ^= u;
	u = t[2] ^ ROL2(t[4], 1);
	a[3] ^= u;
	a[8] ^= u;
	a[13] ^= u;
	a[18] ^= u;
	a[23] ^= u;
	u = t[3] ^ ROL2(t[0], 1);
	a[4] ^= u;
	a[9] ^= u;
	a[14] ^= u;
	a[19] ^= u;
	a[24] ^= u;

	// Rho Pi

	t[0]  = a[0];
	t[10] = ROL2(a[1], 1);
	t[20] = ROL2(a[2], 62);
	t[5]  = ROL2(a[3], 28);
	t[15] = ROL2(a[4], 27);
	
	t[16] = ROL2(a[5], 36);
	t[1]  = ROL2(a[6], 44);
	t[11] = ROL2(a[7], 6);
	t[21] = ROL2(a[8], 55);
	t[6]  = ROL2(a[9], 20);
	
	t[7]  = ROL2(a[10], 3);
	t[17] = ROL2(a[11], 10);
	t[2]  = ROL2(a[12], 43);
	t[12] = ROL2(a[13], 25);
	t[22] = ROL2(a[14], 39);
	
	t[23] = ROL2(a[15], 41);
	t[8]  = ROL2(a[16], 45);
	t[18] = ROL2(a[17], 15);
	t[3]  = ROL2(a[18], 21);
	t[13] = ROL2(a[19], 8);
	
	t[14] = ROL2(a[20], 18);
	t[24] = ROL2(a[21], 2);
	t[9]  = ROL2(a[22], 61);
	t[19] = ROL2(a[23], 56);
	t[4]  = ROL2(a[24], 14);

	// Chi
	chi(a, 0, t);

	// Iota
	a[0] ^= Keccak_f1600_RC[r];

	chi(a, 5, t);
	chi(a, 10, t);
	chi(a, 15, t);
	chi(a, 20, t);
}

static void keccak_f1600_no_absorb(uint2* a)
{

	#pragma nounroll
    for (uint r = 0; r < 24;)
	{
		keccak_f1600_round(a, r++);
	} 
}


__attribute__((reqd_work_group_size(WORKSIZE, 1, 1)))
__kernel void search(__constant uint2* header, __global uint* output, const ulong target)
{
	uint gid = get_global_id(0);
    uint tid = get_local_id(0);

	uint2 pdata[25] = {0};
	uchar * pdata_uchar = (uchar *)&pdata;
	ulong * pdata_ulong = (ulong *)&pdata;

	int vector[64];

    for (int i = 0; i < 10; i++) {
        pdata[i] = header[i];
    }

	pdata[9].s1 = gid;

    pdata_uchar[80] = 0x06;
    pdata_uchar[135] = 0x80;

    keccak_f1600_no_absorb(pdata_uchar);

	for (int i = 0; i < 32; ++i) {
        vector[(2*i)] = (pdata_uchar[i] >> 4);
        vector[(2*i+1)] = (pdata_uchar[i] & 0xF);
    }

	/*for (int i = 0; i < 8; i++) {
		sum2_1 = 0;
		sum2_2 = 0;

		for (int j = 0; j < 32; j++) {
		     sum2_1 = mad24(matrix[(i * 2 + 0) * 32 + j],vector2[j],sum2_1);
			 sum2_2 = mad24(matrix[(i * 2 + 1) * 32 + j],vector2[j],sum2_2);
        }

		sum1_1 = sum2_1.s0 + sum2_1.s1;
		sum1_2 = sum2_2.s0 + sum2_2.s1;


		sum1_1 = sum1_1 >> 10;
		sum1_2 = sum1_2 >> 10;

		hash_second[i] = (sum1_1 << 4) | sum1_2;
    }*/

	CALCMATRIX

	for (int i = 0; i < 32; i++) {
		pdata_uchar[i] = pdata_uchar[i] ^ (((sum1_1[i] >> 10) << 4) | (sum1_2[i] >> 10));
    }

	for (int i = 4; i < 25; i++) {
		pdata[i] = 0;
	}

    pdata_uchar[32] = 0x06;
    pdata_uchar[135] = 0x80;

    keccak_f1600_no_absorb(pdata_uchar);

    if (pdata_ulong[3] <= target) {
		output[atomic_inc(output + 255)] = SWAP4(gid);
	}


	//barrier(CLK_GLOBAL_MEM_FENCE);
	
}
