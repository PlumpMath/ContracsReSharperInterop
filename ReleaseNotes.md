﻿### 1.0.4.0
* Contracts in contstuctors are not detected
* Ignore static read only fields
* Fixed adding usings when usings are only inside namespaces
### 1.0.3.0
* Create ContractClass and ContractInvariantMethod
* Create contracts for items with NotNull attributes.
* Support ContractClassFor with generic base classes
### 1.0.2.0
* ContractClassFor: Existing attribute must be checked on base class, else refactoring will be offered always
* Special check for interfaces to cover explicit interface implementations.
### 1.0.1.0
* Contracts on the "ContractClass" must be annotated on the "ContractClassFor", not on the "ContractClass"
### 1.0.0.0
* Initial