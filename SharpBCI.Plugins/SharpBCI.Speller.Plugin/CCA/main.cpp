#include <iostream>
#include <fstream>
#include <vector>
#include <limits>
#include <map>
#include <mutex>

#include <Eigen/Core>
#include <unsupported/Eigen/MatrixFunctions>

#include "ExportHeader.h"

using namespace std;
using namespace Eigen;

unsigned long long index = 0;
map<unsigned long long, MatrixXd> matrices;
map<unsigned long long, MatrixXd> qr_matrices;
mutex mat_mutex;

VectorXd column_means(MatrixXd &mat) {
	const auto rows = mat.rows();
	const auto cols = mat.cols();
	VectorXd means(cols);

	for (auto c = 0; c < cols; c++)
	{
		double sum = 0;
		for (auto r = 0; r < rows; r++)
			sum += mat(r, c);
		means[c] = static_cast<float>(sum / rows);
	}
	return means;
}

// Center the variables
void centerize(MatrixXd &mat){
	const auto means = column_means(mat);
	const auto rows = mat.rows();
	for (auto r = 0; r < rows; r++)
		mat.row(r) -= means;
}

MatrixXd economy_qr(const MatrixXd &mat) {
	const auto rows = mat.rows();
	const auto cols = mat.cols();
	FullPivHouseholderQR<MatrixXd> qr(mat);
	MatrixXd q(qr.matrixQ());
	if (rows > cols) {
		return MatrixXd(q * MatrixXd::Identity(rows, cols));
	}
	return q;
}

VectorXd cca(MatrixXd &x, MatrixXd &y) {
	const auto n = x.rows();
	if (y.rows() != n)
		return VectorXd();
	centerize(x);
	centerize(y);

	const auto qx = economy_qr(x);
	const auto qy = economy_qr(y);

	BDCSVD<MatrixXd> svd(qx.transpose() * qy);

	return VectorXd(svd.singularValues());
}

MatrixXd cca_qr(MatrixXd &mat) {
	centerize(mat);
	return economy_qr(mat);
}

VectorXd cca_svd(MatrixXd &qx, MatrixXd &qy) {
	BDCSVD<MatrixXd> svd(qx.transpose() * qy);
	return VectorXd(svd.singularValues());
}

double max_in_vector(VectorXd &vec) {
	auto max_value = -numeric_limits<double>::infinity();
	for (size_t i = 0; i < vec.size(); i++)
		max_value = max(max_value, vec[i]);
	return max(min(max_value, 1.0), 0.0);
}

void convert_matrix(const t_mat t_mat, MatrixXd &mat) {
	mat.resize(t_mat.rows, t_mat.cols);
	auto ptr = static_cast<double*>(t_mat.ptr);
	for (size_t r = 0; r < t_mat.rows; r++)
		for (size_t c = 0; c < t_mat.cols; c++)
		{
			mat(r, c) = *ptr;
			ptr++;
		}
}

void retrieve_allocated_matrix(const unsigned long long id, MatrixXd &mat) 
{
	lock_guard<mutex> lock(mat_mutex);
	const auto it = matrices.find(id);
	if (it == matrices.end()) {
		throw std::exception("not found");
	}
	mat = it->second;
}

auto retrieve_matrix(const t_mat t_mat, MatrixXd& mat) -> void
{
	if (t_mat.id > 0)
		retrieve_allocated_matrix(t_mat.id, mat);
	else
		convert_matrix(t_mat, mat);
}

void retrieve_cca_qr_matrix(const t_mat t_mat, MatrixXd &qr_mat) 
{
	if (t_mat.id > 0) {
		lock_guard<mutex> lock(mat_mutex);
		const auto it = qr_matrices.find(t_mat.id);
		if (it != qr_matrices.end()) {
			qr_mat = it->second;
			return;
		}
	}
	if (t_mat.id > 0) {
		retrieve_allocated_matrix(t_mat.id, qr_mat);
		qr_mat = cca_qr(qr_mat);
		lock_guard<mutex> lock(mat_mutex);
		qr_matrices[t_mat.id] = qr_mat;
	}
	else 
	{
		convert_matrix(t_mat, qr_mat);
		qr_mat = cca_qr(qr_mat);
	}
}

void _stdcall compute_cca_qr(const unsigned long long id)
{
	MatrixXd matrix;
	retrieve_allocated_matrix(id, matrix);
	const auto qr_mat = cca_qr(matrix);
	lock_guard<mutex> lock(mat_mutex);
	qr_matrices[id] = qr_mat;
}

unsigned long long _stdcall alloc_matrix(const t_mat mat)
{
	lock_guard<mutex> lock(mat_mutex);
	MatrixXd matrix;
	convert_matrix(mat, matrix);
	while(true)
	{
		const auto id = ++index;
		if (id <= 0)
		{
			index = 0;
			continue;
		}
		if (matrices.find(id) != matrices.end())
			continue;
		matrices[id] = matrix;
		return id;
	}
}

void _stdcall delete_matrix(const unsigned long long index) 
{
	lock_guard<mutex> lock(mat_mutex);
	matrices.erase(index);
	qr_matrices.erase(index);
}

void _stdcall clear_matrices() 
{
	lock_guard<mutex> lock(mat_mutex);
	matrices.clear();
	qr_matrices.clear();
	index = 0;
}

double _stdcall canonical_correlation(const t_mat x, const t_mat y)
{
	MatrixXd qx;
	retrieve_cca_qr_matrix(x, qx);
	MatrixXd qy;
	retrieve_cca_qr_matrix(y, qy);
	auto vector = cca_svd(qx, qy);
	return max_in_vector(vector);
}

// ReSharper disable CppInconsistentNaming
double minimum_energy_combination_power(const MatrixXd Y, const MatrixXd X)
{
	const auto xt = X.transpose();
	MatrixXd Y1 = Y - X * (xt * X).inverse() * xt * Y;
	SelfAdjointEigenSolver<MatrixXd> eigen_solver(Y1.transpose() * Y1);
	const auto count = eigen_solver.eigenvalues().rows();
	auto eigenValues = eigen_solver.eigenvalues();
	auto eigenVectors = eigen_solver.eigenvectors();
	vector<pair<double, VectorXd>> eigens;
	eigens.reserve(count);
	for (auto i = 0; i < count; i++)
		eigens.emplace_back(eigenValues(i, 0), eigenVectors.col(i));
	sort(eigens.begin(), eigens.end(), [](const pair<double, VectorXd> & a, const pair<double, VectorXd> & b) -> bool { return a.first < b.first; });
	MatrixXd W;
	W.resize(eigenVectors.rows(), eigenVectors.cols());
	auto c = 0;
	for (auto eigen : eigens)
	{
		const auto sqrtEigenVal = sqrt(eigen.first);
		for (auto i = 0; i < eigen.second.count(); i++)
			W(i, c) = eigen.second[i] / sqrtEigenVal;
		c++;
	}

	auto S = Y * W;
	auto p = 0.0;
	for (auto l = 0; l < S.cols(); l++)
		for (auto k = 0; k < X.cols(); k++)
			p += pow(abs(X.col(k).transpose() * S.col(l)), 2);
	return p / (static_cast<double>(S.cols()) * static_cast<double>(X.cols()));
}

double _stdcall minimum_energy_combination(const t_mat x, const t_mat y)
{
	MatrixXd Y;
	retrieve_matrix(x, Y);
	MatrixXd X;
	retrieve_matrix(y, X);
	return minimum_energy_combination_power(Y, X);
}
// ReSharper restore CppInconsistentNaming

vector<string> split(const string &str, char delimiter) {
	istringstream iss(str); string tmp; vector<string> res;
	while (getline(iss, tmp, delimiter)) res.push_back(tmp);
	return res;
}

void load_data(const string &filename, MatrixXd &mat) {
	ifstream ifs;
	string line;

	ifs.open(filename.c_str());
	if (!ifs) {
		cerr << "Can't read input file " << endl;
	}

	vector<VectorXd> rows;

	auto column = -1;

	cout << "reading" << endl;
	while (getline(ifs, line)) {
		auto strings = split(line, ',');
		if (strings.empty()) {
			continue;
		}
		column = strings.size();

		VectorXd row(strings.size());
		for (unsigned int i = 0; i < strings.size(); i++) {
			row[i] = atof(strings[i].c_str());
		}
		rows.push_back(row);
	}
	ifs.close();

	mat.resize(rows.size(), column);

	for (unsigned int i = 0; i < rows.size(); i++) {
		for (auto j = 0; j < column; j++)
			mat(i, j) = rows[i][j];
	}

}

int main(int argc, char const *argv[])
{
	MatrixXd x;
	MatrixXd y;
	load_data("E:/X.csv", x);
	load_data("E:/Y.csv", y);

	auto qx = cca_qr(x);
	auto qy = cca_qr(y);
	auto vector = cca_svd(qx, qy);

	cout << "R:\n" << max_in_vector(vector) << endl;
	cout << "power:\n" << minimum_energy_combination_power(x, y) << endl;
	system("pause");
}
