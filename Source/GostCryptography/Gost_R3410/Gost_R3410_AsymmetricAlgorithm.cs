﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Permissions;

using GostCryptography.Asn1.Gost.Gost_R3410;
using GostCryptography.Base;
using GostCryptography.Gost_R3411;
using GostCryptography.Native;
using GostCryptography.Properties;
using GostCryptography.Reflection;

namespace GostCryptography.Gost_R3410
{
	/// <inheritdoc cref="Gost_R3410_AsymmetricAlgorithmBase{TKeyParams,TKeyAlgorithm}" />
	[SecurityCritical]
	[SecuritySafeCritical]
	public abstract class Gost_R3410_AsymmetricAlgorithm<TKeyParams, TKeyAlgorithm> : Gost_R3410_AsymmetricAlgorithmBase<TKeyParams, TKeyAlgorithm>, ICspAsymmetricAlgorithm
		where TKeyParams : Gost_R3410_KeyExchangeParams
		where TKeyAlgorithm : Gost_R3410_KeyExchangeAlgorithm
	{
		public const int DefaultKeySize = 512;
		public static readonly KeySizes[] DefaultLegalKeySizes = { new KeySizes(DefaultKeySize, DefaultKeySize, 0) };


		/// <inheritdoc />
		[SecurityCritical]
		[SecuritySafeCritical]
		protected Gost_R3410_AsymmetricAlgorithm()
		{
			LegalKeySizesValue = DefaultLegalKeySizes;
			_providerParameters = CreateDefaultProviderParameters();
			InitKeyContainer(_providerParameters, out _isRandomKeyContainer);
		}

		/// <inheritdoc />
		[SecurityCritical]
		[SecuritySafeCritical]
		[ReflectionPermission(SecurityAction.Assert, MemberAccess = true)]
		protected Gost_R3410_AsymmetricAlgorithm(ProviderTypes providerType) : base(providerType)
		{
			LegalKeySizesValue = DefaultLegalKeySizes;
			_providerParameters = CreateDefaultProviderParameters();
			InitKeyContainer(_providerParameters, out _isRandomKeyContainer);
		}

		/// <summary>
		/// Конструктор.
		/// </summary>
		/// <param name="providerParameters">Параметры криптографического провайдера.</param>
		[SecurityCritical]
		[SecuritySafeCritical]
		protected Gost_R3410_AsymmetricAlgorithm(CspParameters providerParameters) : base((ProviderTypes)providerParameters.ProviderType)
		{
			LegalKeySizesValue = DefaultLegalKeySizes;
			_providerParameters = CopyExistingProviderParameters(providerParameters);
			InitKeyContainer(_providerParameters, out _isRandomKeyContainer);
		}


		private readonly CspParameters _providerParameters;
		private readonly bool _isRandomKeyContainer;
		private bool _isPersistentKey;
		private bool _isPublicKeyOnly;

		[SecurityCritical]
		private SafeProvHandleImpl _providerHandle;

		[SecurityCritical]
		private volatile SafeKeyHandleImpl _keyHandle;


		/// <summary>
		/// Приватный дескриптор провайдера.
		/// </summary>
		internal SafeProvHandleImpl InternalProvHandle
		{
			[SecurityCritical]
			get
			{
				GetKeyPair();

				return _providerHandle;
			}
		}

		/// <summary>
		/// Дескрипор провайдера.
		/// </summary>
		public IntPtr ProviderHandle
		{
			[SecurityCritical]
			[SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
			get { return InternalProvHandle.DangerousGetHandle(); }
		}

		/// <summary>
		/// Приватный дескриптор ключа.
		/// </summary>
		internal SafeKeyHandleImpl InternalKeyHandle
		{
			[SecurityCritical]
			get
			{
				GetKeyPair();

				return _keyHandle;
			}
		}

		/// <summary>
		/// Дескриптор ключа.
		/// </summary>
		public IntPtr KeyHandle
		{
			[SecurityCritical]
			[SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
			get { return InternalKeyHandle.DangerousGetHandle(); }
		}

		/// <inheritdoc />
		public override int KeySize
		{
			[SecuritySafeCritical]
			get
			{
				GetKeyPair();

				return DefaultKeySize;
			}
		}

		/// <summary>
		/// Хранить ключ в криптографическом провайдере.
		/// </summary>
		public bool IsPersistentKey
		{
			[SecuritySafeCritical]
			get
			{
				if (_providerHandle == null)
				{
					lock (this)
					{
						if (_providerHandle == null)
						{
							_providerHandle = CreateProviderHandle(_providerParameters, _isRandomKeyContainer);
						}
					}
				}

				return _isPersistentKey;
			}
			[SecuritySafeCritical]
			set
			{
				var currentValue = IsPersistentKey;

				if (currentValue != value)
				{
					var keyContainerPermission = new KeyContainerPermission(KeyContainerPermissionFlags.NoFlags);
					var containerAccessEntry = new KeyContainerPermissionAccessEntry(_providerParameters, value ? KeyContainerPermissionFlags.Create : KeyContainerPermissionFlags.Delete);
					keyContainerPermission.AccessEntries.Add(containerAccessEntry);
					keyContainerPermission.Demand();

					_isPersistentKey = value;
					_providerHandle.DeleteOnClose = !_isPersistentKey;
				}
			}
		}

		/// <summary>
		/// Имеется доступ только к открытому ключу.
		/// </summary>
		public bool IsPublicKeyOnly
		{
			[SecuritySafeCritical]
			get
			{
				GetKeyPair();

				return _isPublicKeyOnly;
			}
		}

		/// <inheritdoc />
		public CspKeyContainerInfo CspKeyContainerInfo
		{
			[SecuritySafeCritical]
			get
			{
				GetKeyPair();

				return CspKeyContainerInfoHelper.CreateCspKeyContainerInfo(_providerParameters, _isRandomKeyContainer);
			}
		}


		/// <inheritdoc />
		[SecuritySafeCritical]
		public byte[] ExportCspBlob(bool includePrivateParameters)
		{
			GetKeyPair();

			if (includePrivateParameters)
			{
				throw ExceptionUtility.CryptographicException(Resources.UserExportBulkBlob);
			}

			return CryptoApiHelper.ExportCspBlob(_keyHandle, SafeKeyHandleImpl.InvalidHandle, Constants.PUBLICKEYBLOB);
		}

		/// <inheritdoc />
		[SecuritySafeCritical]
		public void ImportCspBlob(byte[] importedKeyBytes)
		{
			if (importedKeyBytes == null)
			{
				throw ExceptionUtility.ArgumentNull(nameof(importedKeyBytes));
			}

			if (!IsPublicKeyBlob(importedKeyBytes))
			{
				throw ExceptionUtility.Argument(nameof(importedKeyBytes), Resources.UserImportBulkBlob);
			}

			var hProv = CryptoApiHelper.GetProviderHandle(ProviderType);

			_providerParameters.KeyNumber = CryptoApiHelper.ImportCspBlob(importedKeyBytes, hProv, SafeKeyHandleImpl.InvalidHandle, out var hKey);
			_providerHandle = hProv;
			_keyHandle = hKey;

			_isPublicKeyOnly = true;
		}

		private static bool IsPublicKeyBlob(byte[] importedKeyBytes)
		{
			if ((importedKeyBytes[0] != Constants.PUBLICKEYBLOB) || (importedKeyBytes.Length < 12))
			{
				return false;
			}

			var gostKeyMask = BitConverter.GetBytes(Constants.GR3410_1_MAGIC);

			return (importedKeyBytes[8] == gostKeyMask[0])
				   && (importedKeyBytes[9] == gostKeyMask[1])
				   && (importedKeyBytes[10] == gostKeyMask[2])
				   && (importedKeyBytes[11] == gostKeyMask[3]);
		}


		/// <summary>
		/// Вычисляет цифровую подпись.
		/// </summary>
		public override byte[] CreateSignature(byte[] hash)
		{
			return SignHash(hash);
		}

		/// <summary>
		/// Вычисляет цифровую подпись.
		/// </summary>
		[SecuritySafeCritical]
		public byte[] CreateSignature(byte[] data, object hashAlgorithm)
		{
			var hash = CryptographyUtils.ObjToHashAlgorithm(hashAlgorithm).ComputeHash(data);
			return SignHash(hash);
		}

		/// <summary>
		/// Вычисляет цифровую подпись.
		/// </summary>
		[SecuritySafeCritical]
		public byte[] CreateSignature(Stream data, object hashAlgorithm)
		{
			var hash = CryptographyUtils.ObjToHashAlgorithm(hashAlgorithm).ComputeHash(data);
			return SignHash(hash);
		}

		/// <summary>
		/// Вычисляет цифровую подпись.
		/// </summary>
		[SecuritySafeCritical]
		public byte[] CreateSignature(byte[] data, int dataOffset, int dataLength, object hashAlgorithm)
		{
			var hash = CryptographyUtils.ObjToHashAlgorithm(hashAlgorithm).ComputeHash(data, dataOffset, dataLength);
			return SignHash(hash);
		}

		[SecuritySafeCritical]
		private byte[] SignHash(byte[] hash)
		{
			if (hash == null)
			{
				throw ExceptionUtility.ArgumentNull(nameof(hash));
			}

			if (hash.Length != 32)
			{
				throw ExceptionUtility.ArgumentOutOfRange(nameof(hash), Resources.InvalidHashSize);
			}

			if (IsPublicKeyOnly)
			{
				throw ExceptionUtility.CryptographicException(Resources.NoPrivateKey);
			}

			GetKeyPair();

			if (!CspKeyContainerInfo.RandomlyGenerated)
			{
				var keyContainerPermission = new KeyContainerPermission(KeyContainerPermissionFlags.NoFlags);
				var keyContainerAccessEntry = new KeyContainerPermissionAccessEntry(_providerParameters, KeyContainerPermissionFlags.Sign);
				keyContainerPermission.AccessEntries.Add(keyContainerAccessEntry);
				keyContainerPermission.Demand();
			}

			using (var hashAlgorithm = (Gost_R3411_HashAlgorithm)CreateHashAlgorithm())
			{
				return CryptoApiHelper.SignValue(_providerHandle, hashAlgorithm.SafeHandle, _providerParameters.KeyNumber, hash);
			}
		}


		/// <summary>
		/// Проверяет цифровую подпись.
		/// </summary>
		public override bool VerifySignature(byte[] hash, byte[] signature)
		{
			return VerifyHash(hash, signature);
		}

		/// <summary>
		/// Проверяет цифровую подпись.
		/// </summary>
		[SecuritySafeCritical]
		public bool VerifySignature(byte[] buffer, object hashAlgorithm, byte[] signature)
		{
			var hash = CryptographyUtils.ObjToHashAlgorithm(hashAlgorithm).ComputeHash(buffer);
			return VerifyHash(hash, signature);
		}

		/// <summary>
		/// Проверяет цифровую подпись.
		/// </summary>
		[SecuritySafeCritical]
		public bool VerifySignature(Stream inputStream, object hashAlgorithm, byte[] signature)
		{
			var hash = CryptographyUtils.ObjToHashAlgorithm(hashAlgorithm).ComputeHash(inputStream);
			return VerifyHash(hash, signature);
		}

		/// <summary>
		/// Проверяет цифровую подпись.
		/// </summary>
		public bool VerifySignature(byte[] data, int dataOffset, int dataLength, object hashAlgorithm, byte[] signature)
		{
			var hash = CryptographyUtils.ObjToHashAlgorithm(hashAlgorithm).ComputeHash(data, dataOffset, dataLength);
			return VerifyHash(hash, signature);
		}

		[SecuritySafeCritical]
		private bool VerifyHash(byte[] hash, byte[] signature)
		{
			if (hash == null)
			{
				throw ExceptionUtility.ArgumentNull(nameof(hash));
			}

			if (signature == null)
			{
				throw ExceptionUtility.ArgumentNull(nameof(signature));
			}

			if (hash.Length != 32)
			{
				throw ExceptionUtility.ArgumentOutOfRange(Resources.InvalidHashSize);
			}

			GetKeyPair();

			using (var hashAlgorithm = (Gost_R3411_HashAlgorithm)CreateHashAlgorithm())
			{
				return CryptoApiHelper.VerifySign(_providerHandle, hashAlgorithm.SafeHandle, _keyHandle, hash, signature);
			}
		}


		/// <inheritdoc />
		[SecuritySafeCritical]
		public override TKeyAlgorithm CreateKeyExchange(TKeyParams keyParameters)
		{
			GetKeyPair();

			return CreateKeyExchangeAlgorithm(ProviderType, _providerHandle, _keyHandle, (TKeyParams)keyParameters.Clone());
		}


		/// <inheritdoc />
		[SecuritySafeCritical]
		public override TKeyParams ExportParameters(bool includePrivateKey)
		{
			if (includePrivateKey)
			{
				throw ExceptionUtility.NotSupported(Resources.UserExportBulkKeyNotSupported);
			}

			GetKeyPair();

			return CryptoApiHelper.ExportPublicKey(_keyHandle, CreateKeyExchangeParams());
		}

		/// <inheritdoc />
		[SecuritySafeCritical]
		public override void ImportParameters(TKeyParams keyParameters)
		{
			if (keyParameters.PrivateKey != null)
			{
				throw ExceptionUtility.NotSupported(Resources.UserImportBulkKeyNotSupported);
			}

			_keyHandle.TryDispose();

			_providerHandle = CryptoApiHelper.GetProviderHandle(ProviderType);
			_keyHandle = CryptoApiHelper.ImportPublicKey(_providerHandle, keyParameters.Clone());

			_isPublicKeyOnly = true;
		}


		/// <summary>
		/// Установка пароля доступа к контейнеру.
		/// </summary>
		[SecuritySafeCritical]
		[SecurityPermission(SecurityAction.Assert, UnmanagedCode = true)]
		public void SetContainerPassword(SecureString password)
		{
			if (IsPublicKeyOnly)
			{
				throw ExceptionUtility.CryptographicException(Resources.NoPrivateKey);
			}

			GetKeyPair();
			SetSignatureKeyPassword(_providerHandle, password, _providerParameters.KeyNumber);
		}


		/// <inheritdoc />
		[SecuritySafeCritical]
		protected override void Dispose(bool disposing)
		{
			_keyHandle.TryDispose();

			if (!_isPublicKeyOnly)
			{
				_providerHandle.TryDispose();
			}

			base.Dispose(disposing);
		}


		// Helpers

		[SecurityCritical]
		private void GetKeyPair()
		{
			if (_keyHandle == null)
			{
				lock (this)
				{
					if (_keyHandle == null)
					{
						SafeProvHandleImpl providerHandle;
						SafeKeyHandleImpl keyHandle;

						GetKeyPairValue(_providerParameters, _isRandomKeyContainer, out providerHandle, out keyHandle);

						_providerHandle = providerHandle;
						_keyHandle = keyHandle;

						_isPersistentKey = true;
					}
				}
			}
		}


		private void GetKeyPairValue(CspParameters providerParams, bool randomKeyContainer, out SafeProvHandleImpl providerHandle, out SafeKeyHandleImpl keyHandle)
		{
			SafeProvHandleImpl resultProviderHandle = null;
			SafeKeyHandleImpl resultKeyHandle = null;

			try
			{
				resultProviderHandle = CreateProviderHandle(providerParams, randomKeyContainer);

				if (providerParams.ParentWindowHandle != IntPtr.Zero)
				{
					CryptoApiHelper.SetProviderParameter(resultProviderHandle, providerParams.KeyNumber, Constants.PP_CLIENT_HWND, providerParams.ParentWindowHandle);
				}
				else if (providerParams.KeyPassword != null)
				{
					SetSignatureKeyPassword(resultProviderHandle, providerParams.KeyPassword, providerParams.KeyNumber);
				}

				try
				{
					resultKeyHandle = CryptoApiHelper.GetUserKey(resultProviderHandle, providerParams.KeyNumber);
				}
				catch (Exception exception)
				{
					var errorCode = Marshal.GetHRForException(exception);

					if (errorCode != 0)
					{
						if (((providerParams.Flags & CspProviderFlags.UseExistingKey) != CspProviderFlags.NoFlags) || (errorCode != Constants.NTE_NO_KEY))
						{
							throw;
						}

						resultKeyHandle = CryptoApiHelper.GenerateKey(resultProviderHandle, providerParams.KeyNumber, providerParams.Flags);
					}
				}

				var keyAlgIdInverted = CryptoApiHelper.GetKeyParameter(resultKeyHandle, Constants.KP_ALGID);
				var keyAlgId = keyAlgIdInverted[0] | (keyAlgIdInverted[1] << 8) | (keyAlgIdInverted[2] << 16) | (keyAlgIdInverted[3] << 24);

				if ((keyAlgId != ExchangeAlgId) && (keyAlgId != SignatureAlgId))
				{
					throw ExceptionUtility.NotSupported(Resources.KeyAlgorithmNotSupported);
				}
			}
			catch (Exception)
			{
				resultProviderHandle?.Close();
				resultKeyHandle?.Close();
				throw;
			}

			providerHandle = resultProviderHandle;
			keyHandle = resultKeyHandle;
		}

		private static SafeProvHandleImpl CreateProviderHandle(CspParameters providerParams, bool randomKeyContainer)
		{
			SafeProvHandleImpl propvoderHandle = null;

			var keyContainerPermission = new KeyContainerPermission(KeyContainerPermissionFlags.NoFlags);

			try
			{
				propvoderHandle = CryptoApiHelper.OpenProvider(providerParams);
			}
			catch (Exception exception)
			{
				var errorCode = Marshal.GetHRForException(exception);

				if (errorCode != 0)
				{
					if (((providerParams.Flags & CspProviderFlags.UseExistingKey) != CspProviderFlags.NoFlags)
						|| ((errorCode != Constants.NTE_KEYSET_NOT_DEF)
							&& (errorCode != Constants.NTE_BAD_KEYSET)
							&& (errorCode != Constants.SCARD_W_CANCELLED_BY_USER)))
					{
						throw ExceptionUtility.CryptographicException(errorCode);
					}

					if (!randomKeyContainer)
					{
						var containerAccessEntry = new KeyContainerPermissionAccessEntry(providerParams, KeyContainerPermissionFlags.Create);
						keyContainerPermission.AccessEntries.Add(containerAccessEntry);
						keyContainerPermission.Demand();
					}

					propvoderHandle = CryptoApiHelper.CreateProvider(providerParams);

					return propvoderHandle;
				}
			}

			if (!randomKeyContainer)
			{
				var containerAccessEntry = new KeyContainerPermissionAccessEntry(providerParams, KeyContainerPermissionFlags.Open);
				keyContainerPermission.AccessEntries.Add(containerAccessEntry);
				keyContainerPermission.Demand();
			}

			return propvoderHandle;
		}

		private static void SetSignatureKeyPassword(SafeProvHandleImpl hProv, SecureString keyPassword, int keyNumber)
		{
			if (keyPassword == null)
			{
				throw ExceptionUtility.ArgumentNull(nameof(keyPassword));
			}

			var keyPasswordData = Marshal.SecureStringToCoTaskMemAnsi(keyPassword);

			try
			{
				CryptoApiHelper.SetProviderParameter(hProv, keyNumber, Constants.PP_SIGNATURE_PIN, keyPasswordData);
			}
			finally
			{
				if (keyPasswordData != IntPtr.Zero)
				{
					Marshal.ZeroFreeCoTaskMemAnsi(keyPasswordData);
				}
			}
		}


		private CspParameters CreateDefaultProviderParameters(CspProviderFlags defaultFlags = CspProviderFlags.UseMachineKeyStore)
		{
			return new CspParameters(ProviderType.ToInt())
			{
				Flags = defaultFlags
			};
		}

		private CspParameters CopyExistingProviderParameters(CspParameters providerParameters)
		{
			ValidateProviderParameters(providerParameters.Flags);

			return new CspParameters(providerParameters.ProviderType, providerParameters.ProviderName, providerParameters.KeyContainerName)
			{
				Flags = providerParameters.Flags,
				KeyNumber = providerParameters.KeyNumber
			};
		}

		private static void ValidateProviderParameters(CspProviderFlags flags)
		{
			// Ели информацию о провайдере нужно взять из текущего ключа
			if ((flags & CspProviderFlags.UseExistingKey) != CspProviderFlags.NoFlags)
			{
				const CspProviderFlags notExpectedFlags = CspProviderFlags.UseUserProtectedKey
														  | CspProviderFlags.UseArchivableKey
														  | CspProviderFlags.UseNonExportableKey;

				if ((flags & notExpectedFlags) != CspProviderFlags.NoFlags)
				{
					throw ExceptionUtility.Argument(nameof(flags), Resources.InvalidCspProviderFlags);
				}
			}

			// Если пользователь должен сам выбрать ключ (например, в диалоге)
			if ((flags & CspProviderFlags.UseUserProtectedKey) != CspProviderFlags.NoFlags)
			{
				if (!Environment.UserInteractive)
				{
					throw ExceptionUtility.CryptographicException(Resources.UserInteractiveNotSupported);
				}

				new UIPermission(UIPermissionWindow.SafeTopLevelWindows).Demand();
			}
		}

		private void InitKeyContainer(CspParameters providerParameters, out bool randomKeyContainer)
		{
			// Установка типа ключа
			if (providerParameters.KeyNumber == -1)
			{
				providerParameters.KeyNumber = (int)KeyNumber.Exchange;
			}
			else if (providerParameters.KeyNumber == SignatureAlgId)
			{
				providerParameters.KeyNumber = (int)KeyNumber.Signature;
			}
			else if (providerParameters.KeyNumber == ExchangeAlgId)
			{
				providerParameters.KeyNumber = (int)KeyNumber.Exchange;
			}

			// Использовать автогенерированный контейнер
			randomKeyContainer = ((providerParameters.KeyContainerName == null) && ((providerParameters.Flags & CspProviderFlags.UseDefaultKeyContainer) == CspProviderFlags.NoFlags));

			if (randomKeyContainer)
			{
				providerParameters.KeyContainerName = Guid.NewGuid().ToString();
			}
			else
			{
				GetKeyPair();
			}
		}
	}
}