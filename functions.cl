__constant int NAT = 4;
__constant int PASSIVE = 4 * 5;

__constant int LSA = 15;

__constant double A_min = 0.25;
__constant double Const_PC = 4;

int pos2D(const int x, const int y)
{
	int sizeX = get_global_size(0);
	int sizeY = get_global_size(1);

	return x * sizeY + y;

	//return x + y * sizeX;
}

int pos3D(const int x, const int y, const int z)
{
	int sizeX = get_global_size(0);
	int sizeY = get_global_size(1);
	int sizeZ = get_global_size(2);

	return z + y * sizeZ + x * sizeY * sizeZ;

	//return x + y * sizeX + z * sizeX * sizeY;
}

int index2D()
{
	int x = get_global_id(0);
	int y = get_global_id(1);

	int sizeX = get_global_size(0);
	int sizeY = get_global_size(1);

	return x * sizeY + y;
}

int index3D()
{
	int x = get_global_id(0);
	int y = get_global_id(1);
	int z = get_global_id(2);

	int sizeX = get_global_size(0);
	int sizeY = get_global_size(1);
	int sizeZ = get_global_size(2);

	return z + y * sizeZ + x * sizeY * sizeZ;
}

void prepare(const int i2D, const int CT, __global char* CortexA, __global int* CortexT)
{
	if (CortexA[i2D] == 3) {
		CortexA[i2D] = 2;
	}

	if ((CortexA[i2D] == 2) && ((CT - CortexT[i2D]) > NAT)) {
		CortexT[i2D] = CT;
        CortexA[i2D] = 4;
	}
}

void act(const int i2D, const int i3D, const int CT, __global char* CortexA, __global int* CortexT, __global char* C)
{
	int NAct = 0;
	int NActR = 0;
    int NFAct = 0;

    if (
		(C[i3D] > 0) && (CortexA[i2D] <= 0 || (CortexA[i2D] == 4 && (CT - CortexT[i2D]) > PASSIVE))
	) {

		int X = get_global_size(0);
		int Y = get_global_size(1);
	
		int x = get_global_id(0);
		int y = get_global_id(1);
		int z = get_global_id(2);

		for(int x1 = x - LSA; x1 <= x + LSA; x1++) {
            for(int y1 = y - LSA; y1 <= y + LSA; y1++) {

                if (x1 >= 0 && x1 < X && y1 >= 0 && y1 < Y) {

					int p2D = pos2D(x1, y1);
					int p3D = pos3D(x1, y1, z);

                    if (C[p3D] > 0) {

                        NActR += 1;

                        if (CortexA[p2D] == 1 || CortexA[p2D] == 2) {
                            NAct += 1;
                        }
                    else
                        if (CortexA[p2D] == 1 || CortexA[p2D] == 2) {
                            NFAct += 1;
                        }
                    }
                }
            }
        }


		if (NActR > 0) {

			if (((NAct / (double)NActR) > A_min) && ((NFAct / Const_PC) < NAct)) {

				CortexT[i2D] = CT;
				CortexA[i2D] = 3;
			}
		}
	}

	//if (get_global_id(2) == 10 && get_global_id(1) == 20) {
	//CortexA[i2D] = 3;
	//}
}

void makePicWave(const int i2D, const int CT, __write_only image2d_t dstImg, __global char* CortexA, __global int* CortexT)
{
	int x = get_global_id(0);
	int y = get_global_id(1);

	int2 coord = (int2)(x, y);
	uint4 bgra = (uint4)(0,0,0,255);

	char val = CortexA[i2D];
	if (val == 1) {
		bgra.z = 255;
		//bgra.x = bgra.y = 0;
	
	} else if (val == 2) {
		//int gray = (int) (((1 - (CT - CortexT[i2D]) / (double)NAT)) / (double)2 + 0.5) * 255;

		bgra.x = bgra.y = bgra.z = 150;

	} else if (val == 3) {
		bgra.x = bgra.y = bgra.z = 255;

	} else if (val == 4) {
		//bgra.x = 255;
		//bgra.y = bgra.z = 0;

	} else {
		bgra.x = bgra.y = bgra.z = 0;
	}
	//bgra.w = 255;

	write_imageui(dstImg, coord, bgra);
}

__kernel void Do(__write_only image2d_t dstImg, const int CT, __global char* CortexA, __global int* CortexT, __global char* C, const int X, const int Y, const int Z)
{

	int i2D = index2D();
	int i3D = index3D();

	prepare(i2D, CT, CortexA, CortexT);
	
	barrier(CLK_GLOBAL_MEM_FENCE);

	act(i2D, i3D, CT, CortexA, CortexT, C);

	barrier(CLK_GLOBAL_MEM_FENCE);

	makePicWave(i2D, CT, dstImg, CortexA, CortexT);
}