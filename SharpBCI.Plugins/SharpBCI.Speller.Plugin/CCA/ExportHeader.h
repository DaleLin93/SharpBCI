#pragma once

typedef struct
{
	unsigned long long id;
	void *ptr;
	unsigned int rows;
	unsigned int cols;
} t_mat;

extern "C" __declspec(dllexport) unsigned long long _stdcall alloc_matrix(t_mat mat);

extern "C" __declspec(dllexport) void _stdcall delete_matrix(unsigned long long index);

extern "C" __declspec(dllexport) void _stdcall clear_matrices();

extern "C" __declspec(dllexport) void _stdcall compute_cca_qr(unsigned long long id);

extern "C" __declspec(dllexport) double _stdcall canonical_correlation(t_mat x, t_mat y);

extern "C" __declspec(dllexport) double _stdcall minimum_energy_combination(t_mat x, t_mat y);
