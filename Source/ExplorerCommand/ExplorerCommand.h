#pragma once

#include <Windows.h>
#include <ShObjIdl_core.h>

class __declspec(uuid("C693703E-617D-439A-A14D-76EBC07AE86A")) ExplorerCommand :
    public IExplorerCommand
{
public:
    static HRESULT CreateInstance(REFIID riid, void** ppv);

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv);
    IFACEMETHODIMP_(ULONG) AddRef();
    IFACEMETHODIMP_(ULONG) Release();

    // IExplorerCommand
    IFACEMETHODIMP GetTitle(IShellItemArray* pItemArray, PWSTR* ppszName);
    IFACEMETHODIMP GetIcon(IShellItemArray* pItemArray, PWSTR* ppIcon);
    IFACEMETHODIMP GetToolTip(IShellItemArray* pItemArray, PWSTR* ppToolTip);
    IFACEMETHODIMP GetCanonicalName(GUID* pCommandName);
    IFACEMETHODIMP GetState(IShellItemArray* pItemArray, BOOL okToBeSlow, EXPCMDSTATE* pState);
    IFACEMETHODIMP Invoke(IShellItemArray* pItemArray, IBindCtx* pBindCtx);
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* pFlags);
    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** ppEnum);

private:
    HRESULT InvokeCommand();
    DWORD InvokeThreadProc();

    LONG m_ref{1};
    bool m_pack{false};
    IStream* m_pstmShellItemArray{};
};
