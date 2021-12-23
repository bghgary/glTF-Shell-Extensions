#pragma once

#include <Windows.h>
#include <new>

template <typename T>
class ClassFactory : public IClassFactory
{
public:
    static HRESULT CreateInstance(REFIID riid, void** ppv)
    {
        *ppv = nullptr;

        ClassFactory* pClassFactory = new(std::nothrow) ClassFactory();
        if (pClassFactory == nullptr)
        {
            return E_OUTOFMEMORY;
        }

        HRESULT hr = pClassFactory->QueryInterface(riid, ppv);
        pClassFactory->Release();
        return hr;
    }

    ClassFactory()
    {
        DllAddRef();
    }

    // IUnknown

    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv)
    {
        IUnknown* punk = nullptr;

        if (riid == __uuidof(IUnknown) || riid == __uuidof(IClassFactory))
        {
            punk = static_cast<IClassFactory*>(this);
        }

        *ppv = punk;

        if (punk == nullptr)
        {
            return E_NOINTERFACE;
        }

        punk->AddRef();
        return S_OK;
    }

    IFACEMETHODIMP_(ULONG) AddRef()
    {
        return InterlockedIncrement(&m_ref);
    }

    IFACEMETHODIMP_(ULONG) Release()
    {
        LONG cRef = InterlockedDecrement(&m_ref);
        if (cRef == 0)
        {
            delete this;
        }
        return cRef;
    }

    // IClassFactory

    IFACEMETHODIMP CreateInstance(IUnknown* punkOuter, REFIID riid, void** ppv)
    {
        if (punkOuter != nullptr)
        {
            return CLASS_E_NOAGGREGATION;
        }

        return T::CreateInstance(riid, ppv);
    }

    IFACEMETHODIMP LockServer(BOOL fLock)
    {
        if (fLock)
        {
            DllAddRef();
        }
        else
        {
            DllRelease();
        }

        return S_OK;
    }

private:
    ~ClassFactory()
    {
        DllRelease();
    }

    LONG m_ref{1};
};
