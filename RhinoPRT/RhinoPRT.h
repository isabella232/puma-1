#ifndef RHINOPRT
#define RHINOPRT

#include "version.h"
#include "PRTContext.h"

#include "ModelGenerator.h"

#include "Logger.h"

#include <algorithm>
#include <cassert>
#include <memory>
#include <string>
#include <map>
#include <array>

#pragma comment(lib, "ole32.lib") // Workaround for "combaseapi.h(229): error C2187: syntax error: 'identifier' was unexpected here" when using /permissive-

#define RHINOPRT_API __declspec(dllexport)

namespace RhinoPRT {

	class RhinoPRTAPI {
	public:
		const RuleAttributeUPtr RULE_NOT_FOUND{};

		bool InitializeRhinoPRT();
		void ShutdownRhinoPRT();
		bool IsPRTInitialized();

		void SetRPKPath(const std::wstring &rpk_path);

		int GetRuleAttributeCount();
		RuleAttributes& GetRuleAttributes();

		void AddInitialShape(const std::vector<InitialShape>& shapes);
		void ClearInitialShapes();

		bool GenerateGeometry();

		template<typename T>
		void fillAttributeFromNode(const std::wstring& ruleName, const std::wstring& attrFullName, T value, size_t count = 1);

		void setRuleAttributeValue(const RuleAttributeUPtr& rule, double value, size_t /*count*/);
		void setRuleAttributeValue(const RuleAttributeUPtr& rule, int value, size_t /*count*/);
		void setRuleAttributeValue(const RuleAttributeUPtr& rule, bool value, size_t /*count*/);
		void setRuleAttributeValue(const RuleAttributeUPtr& rule, std::wstring& value, size_t /*count*/);
		void setRuleAttributeValue(const RuleAttributeUPtr& rule, const double* value, const size_t count);
		void setRuleAttributeValue(const RuleAttributeUPtr& rule, bool* value, const size_t count);
		void setRuleAttributeValue(const RuleAttributeUPtr& rule, std::vector<const wchar_t *> value, const size_t /*count*/);

		const Reporting::GroupedReports& getReports() const { return mGroupedReports; }
		Reporting::ReportsVector getReportsOfModel(int initialShapeID);
		
		std::vector<GeneratedModel>& getGenModels();

		std::vector<int> getModelIds();

		void setMaterialGeneration(bool emitMaterial);

	private:

		std::vector<InitialShape> mShapes;
		std::wstring mPackagePath;
		std::vector<pcu::ShapeAttributes> mAttributes;

		pcu::AttributeMapBuilderPtr mAttrBuilder;

		pcu::EncoderOptions options;

		std::unique_ptr<ModelGenerator> mModelGenerator;
		std::vector<GeneratedModel> mGeneratedModels;

		Reporting::GroupedReports mGroupedReports;
	};

	// Global PRT handle
	RhinoPRTAPI& get();

} // namespace RhinoPRT

// Define exposed functions here
extern "C" {

	RHINOPRT_API void GetProductVersion(ON_wString* version_Str);

	RHINOPRT_API bool InitializeRhinoPRT();

	RHINOPRT_API void ShutdownRhinoPRT();

	RHINOPRT_API void SetPackage(const wchar_t* rpk_path);

	RHINOPRT_API bool AddInitialMesh(ON_SimpleArray<const ON_Mesh*>* pMesh);

	RHINOPRT_API void ClearInitialShapes();

	RHINOPRT_API bool Generate();

	RHINOPRT_API bool GetMeshBundle(int initShapeID, ON_SimpleArray<ON_Mesh*>* pMeshArray);

	RHINOPRT_API void GetAllMeshIDs(ON_SimpleArray<int>* pMeshIDs);

	RHINOPRT_API int GetMeshPartCount(int initShapeId);

	RHINOPRT_API int GetRuleAttributesCount();

	RHINOPRT_API bool GetRuleAttribute(int attrIdx, ON_wString* pRule, ON_wString* pName, ON_wString* pNickname, prt::AnnotationArgumentType* type, ON_wString* pGroup);

	RHINOPRT_API void SetRuleAttributeDouble(const wchar_t* rule, const wchar_t* fullName, double value);

	RHINOPRT_API void SetRuleAttributeBoolean(const wchar_t* rule, const wchar_t* fullName, bool value);

	RHINOPRT_API void SetRuleAttributeInteger(const wchar_t* rule, const wchar_t* fullName, int value);

	RHINOPRT_API void SetRuleAttributeString(const wchar_t* rule, const wchar_t* fullName, const wchar_t* value);

	RHINOPRT_API void SetRuleAttributeDoubleArray(const wchar_t* rule, const wchar_t* fullName, ON_SimpleArray<double>* pValueArray);
	
	RHINOPRT_API void SetRuleAttributeBoolArray(const wchar_t* rule, const wchar_t* fullName, ON_SimpleArray<int>* pValueArray);

	RHINOPRT_API void SetRuleAttributeStringArray(const wchar_t* rule, const wchar_t* fullName, ON_ClassArray<ON_wString>* pValueArray);

	RHINOPRT_API void GetReports(int initialShapeId, ON_ClassArray<ON_wString>* pKeysArray,
		ON_SimpleArray<double>* pDoubleReports,
		ON_SimpleArray<bool>* pBoolReports,
		ON_ClassArray<ON_wString>* pStringReports);

	RHINOPRT_API void GetAnnotationTypes(int ruleIdx, ON_SimpleArray<AttributeAnnotation>* pAnnotTypeArray);

	RHINOPRT_API bool GetEnumType(int ruleIdx, int enumIdx, EnumAnnotationType* type);

	RHINOPRT_API bool GetAnnotationEnumDouble(int ruleIdx, int enumIdx, ON_SimpleArray<double>* pArray);

	RHINOPRT_API bool GetAnnotationEnumString(int ruleIdx, int enumIdx, ON_ClassArray<ON_wString>* pArray);

	RHINOPRT_API bool GetAnnotationRange(int ruleIdx, int enumIdx, double* min, double* max, double* stepsize, bool* restricted);

	RHINOPRT_API bool GetMaterial(int initialShapeId, int meshID, int* uvSet,
		ON_ClassArray<ON_wString>* pTexKeys,
		ON_ClassArray<ON_wString>* pTexPaths,
		ON_SimpleArray<int>* pDiffuseColor,
		ON_SimpleArray<int>* pAmbientColor,
		ON_SimpleArray<int>* pSpecularColor,
		double* opacity,
		double* shininess);

	RHINOPRT_API void SetMaterialGenerationOption(bool doGenerate);

}

#endif RHINOPRT