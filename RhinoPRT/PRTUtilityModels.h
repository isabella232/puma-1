#pragma once

#include "utils.h"
#include "ReportAttribute.h"

#include <vector>

const std::wstring INIT_SHAPE_ID_KEY = L"InitShapeIdx";

/**
* The Initial shape that will be given to PRT
*/
class InitialShape {
public:
	InitialShape() = default;
	InitialShape(const std::vector<double> &vertices);
	InitialShape(const double* vertices, int vCount, const int* indices, const int iCount, const int* faceCount, const int faceCountCount);
	InitialShape(const ON_Mesh& mesh);
	~InitialShape() {}

	const int getID() const {
		return mID;
	}

	const double* getVertices() const {
		return mVertices.data();
	}

	size_t getVertexCount() const {
		return mVertices.size();
	}

	const uint32_t* getIndices() const {
		return mIndices.data();
	}

	size_t getIndexCount() const {
		return mIndices.size();
	}

	const uint32_t* getFaceCounts() const {
		return mFaceCounts.data();
	}

	size_t getFaceCountsCount() const {
		return mFaceCounts.size();
	}

protected:

	int mID;
	std::vector<double> mVertices;
	std::vector<uint32_t> mIndices;
	std::vector<uint32_t> mFaceCounts;
};

/**
* The model generated by PRT
*/
class GeneratedModel {
public:
	GeneratedModel(const size_t& initialShapeIdx, const std::vector<double>& vert, const std::vector<uint32_t>& indices,
		const std::vector<uint32_t>& face, const Reporting::ReportMap& rep);

	GeneratedModel() {}
	~GeneratedModel() {}

	const ON_Mesh getMeshFromGenModel() const;

	size_t getInitialShapeIndex() const {
		return mInitialShapeIndex;
	}
	const std::vector<double>& getVertices() const {
		return mVertices;
	}
	const std::vector<uint32_t>& getIndices() const {
		return mIndices;
	}
	const std::vector<uint32_t>& getFaces() const {
		return mFaces;
	}
	const Reporting::ReportMap& getReport() const {
		return mReports;
	}

private:
	size_t mInitialShapeIndex;
	std::vector<double> mVertices;
	std::vector<uint32_t> mIndices;
	std::vector<uint32_t> mFaces;
	Reporting::ReportMap mReports;
};