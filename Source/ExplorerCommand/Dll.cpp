#include "Dll.h"
#include "ClassFactory.h"
#include "ExplorerCommand.h"

namespace
{
    long g_refModule = 0;
}

void DllAddRef()
{
    InterlockedIncrement(&g_refModule);
}

void DllRelease()
{
    InterlockedDecrement(&g_refModule);
}

STDAPI_(BOOL) DllMain(HINSTANCE hInstance, DWORD dwReason, void*)
{
    if (dwReason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(hInstance);
    }

    return TRUE;
}

STDAPI DllCanUnloadNow()
{
    // Only allow the DLL to be unloaded after all outstanding references have been released
    return (g_refModule == 0) ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(REFCLSID clsid, REFIID riid, void **ppv)
{
    if (clsid == __uuidof(ExplorerCommand))
    {
        return ClassFactory<ExplorerCommand>::CreateInstance(riid, ppv);
    }

    return CLASS_E_CLASSNOTAVAILABLE;
}
