#ifndef System_UInt32_H__
#define System_UInt32_H__

#pragma once

#include <il2c.h>

#ifdef __cplusplus
extern "C" {
#endif

/////////////////////////////////////////////////////////////
// System.UInt32

typedef uint32_t System_UInt32;

typedef System_ValueType_VTABLE_DECL__ System_UInt32_VTABLE_DECL__;

extern System_UInt32_VTABLE_DECL__ System_UInt32_VTABLE__;

IL2C_DECLARE_RUNTIME_TYPE(System_UInt32);

extern /* virtual */ System_String* System_UInt32_ToString(uint32_t* this__);
extern /* virtual */ int32_t System_UInt32_GetHashCode(uint32_t* this__);
extern bool System_UInt32_Equals(uint32_t* this__, uint32_t obj);
extern /* virtual */ bool System_UInt32_Equals_1(uint32_t* this__, System_Object* obj);
extern /* static */ bool System_UInt32_TryParse(System_String* s, uint32_t* result);

#ifdef __cplusplus
}
#endif

#endif
